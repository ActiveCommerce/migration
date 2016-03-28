using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web;
using ActiveCommerce.Extensions;
using ActiveCommerce.Orders;
using ActiveCommerce.Orders.Management;
using ActiveCommerce.Orders.States;
using ActiveCommerce.Products;
using ActiveCommerce.PromoEngine;
using ActiveCommerce.Taxes;
using Microsoft.Practices.Unity;
using Sitecore.Diagnostics;
using Sitecore.Ecommerce.DomainModel.Addresses;
using Sitecore.Ecommerce.DomainModel.Payments;
using ReservationTicket = ActiveCommerce.Orders.ReservationTicket;

namespace ActiveCommerce.Migration.Orders
{
    public class MigrationAssistant
    {
        private readonly bool _testOnly;
        private readonly IOrderFactory _orderFactory;
        private readonly IOrderStatesRepository _orderStatesRepository;
        private readonly IOrderRepository<ActiveCommerce.Orders.Order> _orderRepository;

        private IOrderFactory OrderFactory
        {
            get { return _orderFactory; }
        }

        private IOrderStatesRepository OrderStatesRepository
        {
            get { return _orderStatesRepository; }
        }

        public MigrationAssistant(bool testOnly)
        {
            _testOnly = testOnly;
            _orderFactory = Sitecore.Ecommerce.Context.Entity.Resolve<IOrderFactory>();
            _orderStatesRepository = Sitecore.Ecommerce.Context.Entity.Resolve<IOrderStatesRepository>();
            _orderRepository = Sitecore.Ecommerce.Context.Entity.Resolve<IOrderRepository<ActiveCommerce.Orders.Order>>();
        }

        public int Process()
        {
            var processed = 0;
            var legacyOrders = GetLegacyOrders();
            foreach (var legacyOrder in legacyOrders)
            {
                var order = ConvertOrder(legacyOrder);
                if (!_testOnly)
                {
                    _orderRepository.Add(order);
                    _orderRepository.Flush();
                }
                processed++;
            }
            return processed;
        }

        protected IEnumerable<ActiveCommerce.Orders.Legacy.Order> GetLegacyOrders()
        {
            var manager = Sitecore.Ecommerce.Context.Entity.Resolve<Sitecore.Ecommerce.DomainModel.Orders.IOrderManager<Sitecore.Ecommerce.DomainModel.Orders.Order>>();

            //need at least one condition in the query or the SES OrderManager will throw an error
            var query = new Sitecore.Ecommerce.Search.Query();
            query.AppendField("ItemsInShoppingCart", "-1", Sitecore.Ecommerce.Search.MatchVariant.NotEquals);
            return manager.GetOrders(query).Cast<ActiveCommerce.Orders.Legacy.Order>();
        }

        protected virtual Order ConvertOrder(ActiveCommerce.Orders.Legacy.Order source)
        {
            Assert.ArgumentNotNull(source, "source");
            Assert.IsNotNull(source.CustomerInfo, "customerinfo");
            Assert.IsNotNull(source.Currency, "currency");

            var destination = OrderFactory.CreateOrder();

            destination.ShopContext = Sitecore.Context.Site.Name;
            destination.IssueDate = source.OrderDate;
            destination.OrderId = source.OrderNumber;
            destination.State = GetState(source);

            var payment = GetPayment(source, destination);
            destination.Payments.Add(payment);

            destination.PricingCurrencyCode = source.Currency.Code;
            destination.TaxCurrencyCode = source.Currency.Code;
            destination.TaxTotal = GetTaxTotal(source);
            destination.AnticipatedMonetaryTotal = GetAnticipatedMonetaryTotal(source);
            destination.AllowanceCharge = GetAllowanceCharge(source);
            destination.DestinationCountryCode = source.CustomerInfo.BillingAddress.Country.Code;
            destination.BuyerCustomerParty = GetBuyerCustomerParty(source);
            destination.Delivery = GetDelivery(source, destination);
            destination.FreightForwarderParty = GetFreightForwarderParty(source);

            /**
             * TODO: populate any additional values on the order that you've added.
             * Cast to your order type if necessary.
             */

            foreach (ActiveCommerce.Orders.Legacy.OrderLine cartLine in source.OrderLines)
            {
                var orderLine = GetOrderLine(cartLine, source.Currency.Code, string.Join(", ", source.Discounts));
                orderLine.Order = destination;

                /**
                 * TODO: populate any additional values on the order line that you've added.
                 * Cast to your order line type if necessary.
                 */

                destination.OrderLines.Add(orderLine);
                foreach (var allowance in orderLine.AllowanceCharge)
                {
                    destination.AllowanceCharge.Add(allowance);
                }
            }

            return destination;
        }

        protected virtual State GetState(ActiveCommerce.Orders.Legacy.Order source)
        {
            //TODO: Expand to include all utilized order states
            var legacyState = source.Status.GetType().Name;
            switch (legacyState)
            {
                case "NewOrder":
                    return GetOrderState("New");
                case "Processing":
                    return GetOrderState("Processing");
                case "Pending":
                    return GetOrderState("New", new[] { "Payment Pending" });
                case "Completed":
                    return GetOrderState("Complete");
                default:
                    throw new Exception(string.Format("Unmapped order state: {0}. Add mapping in MigrationAssistant.GetState method.", legacyState));
            }
        }

        protected virtual State GetOrderState(string stateCode, string[] substateCodes = null)
        {

            var state = OrderStatesRepository.GetStates().SingleOrDefault(x => x.Code == stateCode);
            if (state == null)
            {
                throw new ArgumentException(string.Format("Unknown order state: {0}", stateCode));
            }

            if (substateCodes == null || substateCodes.Length == 0)
            {
                return state;
            }

            var substates = state.Substates.Where(x => substateCodes.Contains(x.Code));
            foreach (var substate in substates)
            {
                substate.Active = true;
            }
            return state;
        }

        protected virtual PaymentMeans GetPayment(ActiveCommerce.Orders.Legacy.Order source, ActiveCommerce.Orders.Order destination)
        {
            var means = CreatePaymentMeans(source) ?? OrderFactory.CreatePaymentMeans();
            means.PaymentChannelCode = source.PaymentSystem.Code;
            means.PaymentMeansCode = source.PaymentSystem.Code;
            means.PaymentMeansTitle = source.PaymentSystem.Title;
            means.PaymentDueDate = source.OrderDate;
            means.PaymentID = source.TransactionNumber;
            means.PaymentStatus = PaymentStatus.Succeeded.ToString();
            means.TransactionNumber = source.TransactionNumber;
            means.Order = destination;
            return means;
        }

        protected virtual PaymentMeans CreatePaymentMeans(ActiveCommerce.Orders.Legacy.Order source)
        {
            return source.CreditCardData != null ? GetCreditCardPayment(source) : null;
        }

        protected virtual CreditCardPayment GetCreditCardPayment(ActiveCommerce.Orders.Legacy.Order source)
        {
            var creditCard = source.CreditCardData;
            var means = OrderFactory.CreateCreditCardPayment();
            means.Type = creditCard.CardType;
            means.LastFour = creditCard.CardNumberLastFour;
            means.Expiration = creditCard.ExpirationDate;
            means.ReservationTicket = GetReservationTicket(source);
            return means;
        }

        protected virtual ReservationTicket GetReservationTicket(ActiveCommerce.Orders.Legacy.Order source)
        {
            if (source.AuthorizationCode == null)
            {
                return null;
            }

            var ticket = OrderFactory.CreateReservationTicket();
            ticket.Amount = source.Totals.TotalPriceIncVat;
            ticket.AuthorizationCode = source.AuthorizationCode;
            ticket.InvoiceNumber = source.OrderNumber;
            ticket.TransactionNumber = source.TransactionNumber;

            return ticket;
        }

        protected virtual TaxTotal GetTaxTotal(ActiveCommerce.Orders.Legacy.Order source)
        {
            Assert.ArgumentNotNull(source, "source");
            var taxTotal = OrderFactory.CreateTaxTotal();
            var totals = source.Totals as ActiveCommerce.Prices.OrderTotals;
            Assert.IsNotNull(totals, "ShoppingCart.Totals is not correct type (ActiveCommerce.Prices.OrderTotals). Cannot get tax totals.");

            taxTotal.RoundingAmount = OrderFactory.CreateAmount(0M, source.Currency.Code);
            taxTotal.TaxAmount = OrderFactory.CreateAmount(totals.TaxTotals.Tax, source.Currency.Code);

            uint sequence = 0;
            foreach (var lineItem in totals.TaxTotals.LineItems)
            {
                foreach (var taxJurisdiction in lineItem.Jurisdictions)
                {
                    var total = GetTaxSubTotal(lineItem, taxJurisdiction, source.Currency.Code);
                    total.CalculationSequenceNumeric = sequence;
                    taxTotal.TaxSubtotal.Add(total);
                    sequence++;
                }
            }

            return taxTotal;
        }

        protected virtual TaxSubTotal GetTaxSubTotal(TaxLine taxLine, TaxJurisdiction taxJurisdiction, string currencyCode)
        {
            Assert.ArgumentNotNull(taxLine, "taxLine");
            Assert.ArgumentNotNull(taxJurisdiction, "taxJurisdiction");
            Assert.ArgumentNotNullOrEmpty(currencyCode, "currencyCode");

            var total = OrderFactory.CreateTaxSubTotal();
            total.TaxableAmount = OrderFactory.CreateAmount(taxLine.TaxedAmount, currencyCode);
            total.TaxAmount = OrderFactory.CreateAmount(taxJurisdiction.Tax, currencyCode);
            total.TransactionCurrencyTaxAmount = OrderFactory.CreateAmount(taxJurisdiction.Tax, currencyCode);

            var category = OrderFactory.CreateTaxCategory();
            category.ID = taxLine.ProductCode; // This will be either the actual product code for product tax, "SHIPPING" for shipping tax, or "HANDLING" for handling tax
            category.Name = taxLine.Type; // This will be populated for VAT, blank for North America tax
            category.Percent = taxJurisdiction.Rate * 100;
            category.BaseUnitMeasure = OrderFactory.CreateMeasure();
            category.PerUnitAmount = OrderFactory.CreateAmount(0M, currencyCode);

            var scheme = OrderFactory.CreateTaxScheme();
            scheme.CurrencyCode = currencyCode;
            scheme.ID = taxJurisdiction.Name;
            scheme.Name = taxJurisdiction.Name;
            scheme.TaxTypeCode = taxJurisdiction.Type;

            category.TaxScheme = scheme;
            total.TaxCategory = category;

            return total;
        }

        protected virtual MonetaryTotal GetAnticipatedMonetaryTotal(ActiveCommerce.Orders.Legacy.Order source)
        {
            Assert.ArgumentNotNull(source, "source");
            Assert.IsNotNull(source.Currency, "currency");

            var monetaryTotal = OrderFactory.CreateMonetaryTotal();
            var totals = source.Totals;

            monetaryTotal.AllowanceTotalAmount = OrderFactory.CreateAmount(totals.DiscountExVat, source.Currency.Code);
            monetaryTotal.ChargeTotalAmount = OrderFactory.CreateAmount(source.ShippingPrice, source.Currency.Code);
            monetaryTotal.LineExtensionAmount = OrderFactory.CreateAmount(totals.PriceExVat, source.Currency.Code);
            monetaryTotal.PayableAmount = OrderFactory.CreateAmount(totals.TotalPriceIncVat, source.Currency.Code);
            monetaryTotal.PayableRoundingAmount = OrderFactory.CreateAmount(0M, source.Currency.Code);
            monetaryTotal.PrepaidAmount = OrderFactory.CreateAmount(0M, source.Currency.Code);
            monetaryTotal.TaxExclusiveAmount = OrderFactory.CreateAmount(totals.TotalVat, source.Currency.Code);
            monetaryTotal.TaxInclusiveAmount = OrderFactory.CreateAmount(totals.PriceExVat + totals.TotalVat, source.Currency.Code);

            return monetaryTotal;
        }

        protected virtual ICollection<Sitecore.Ecommerce.OrderManagement.Orders.AllowanceCharge> GetAllowanceCharge(ActiveCommerce.Orders.Legacy.Order source)
        {
            Assert.ArgumentNotNull(source, "source");
            Assert.IsNotNull(source.Currency, "currency");

            // From SES Documentation: http://sdn.sitecore.net/upload/sdn5/products/sefe/ses22/order_manager_cookbook_22-usletter.pdf
            // Discounts:
            //   Details of any discounts or offers that the customer is entitled to.
            //   For each discount you must enter an adjustment reason description code and description in the reason code and description fields.
            //   You can find these codes and descriptions on the UN/EDIFACT website. http://www.unece.org/trade/untdid/d03a/tred/tred4465.htm
            // Charges:
            //   Details of any additional charges incurred by the customer.
            //   For each charge you must enter an adjustment reason description code and description in the reason code
            //   and description fields. You can find these codes and descriptions on the UN/EDIFACT website. 
            //   UN/EDIFACT does not have a predefined code for freight, so instead SES automatically allocates 
            //   the ZZZ Mutually defined code to customer orders that include freight (shipping). 

            var allowances = new List<Sitecore.Ecommerce.OrderManagement.Orders.AllowanceCharge>();
            var totals = source.Totals as ActiveCommerce.Prices.OrderTotals;

            var discounts = string.Join(", ", source.Discounts);

            // Discounts
            if (totals.SubtotalDiscount > 0)
            {
                allowances.Add(GetAllowanceCharge(totals.SubtotalDiscount, discounts, source.Currency.Code));
            }
            if (totals.ShippingDiscount > 0)
            {
                allowances.Add(GetAllowanceCharge(totals.ShippingDiscount, discounts, source.Currency.Code, true));
            }

            // Charges
            var shipping = OrderFactory.CreateAllowanceCharge();
            shipping.ChargeIndicator = true;
            shipping.ShippingIndicator = true;
            shipping.SequenceNumeric = 1M;
            shipping.BaseAmount = OrderFactory.CreateAmount(0M, source.Currency.Code);
            shipping.Amount = OrderFactory.CreateAmount(source.ShippingPrice, source.Currency.Code);
            shipping.AllowanceChargeReasonCode = "ZZZ";
            shipping.AllowanceChargeReason = "Shipping";

            allowances.Add(shipping);

            return allowances;
        }

        protected virtual AllowanceCharge GetAllowanceCharge(decimal amount, string discountCode, string currencyCode, bool shippingIndicator = false)
        {
            var allowance = OrderFactory.CreateAllowanceCharge();
            allowance.ChargeIndicator = false;
            allowance.ShippingIndicator = shippingIndicator;
            allowance.SequenceNumeric = 1M;
            allowance.BaseAmount = OrderFactory.CreateAmount(0M, currencyCode);
            allowance.Amount = OrderFactory.CreateAmount(amount, currencyCode);
            allowance.ID = discountCode;
            allowance.AllowanceChargeReasonCode = discountCode;
            allowance.AllowanceChargeReason = discountCode;
            return allowance;
        }

        protected virtual CustomerParty GetBuyerCustomerParty(ActiveCommerce.Orders.Legacy.Order source)
        {
            Assert.ArgumentNotNull(source, "source");
            Assert.IsNotNull(source.CustomerInfo, "customerinfo");

            var customerParty = OrderFactory.CreateCustomerParty();
            var party = OrderFactory.CreateParty();
            var contact = OrderFactory.CreateContact();

            var list = new List<Sitecore.Ecommerce.Common.Communication>();
            if (!string.IsNullOrEmpty(source.CustomerInfo.Email2))
            {
                var communication = OrderFactory.CreateCommunication();
                communication.Channel = source.CustomerInfo.Email2;
                communication.Value = source.CustomerInfo.Email2;
                communication.Contact = contact;
                list.Add(communication);
            }
            if (!string.IsNullOrEmpty(source.CustomerInfo.Mobile))
            {
                var communication = OrderFactory.CreateCommunication();
                communication.Channel = source.CustomerInfo.Mobile;
                communication.Value = source.CustomerInfo.Mobile;
                communication.Contact = contact;
                list.Add(communication);
            }
            if (list.Any())
            {
                contact.OtherCommunications = list;
            }

            contact.Name = GetFullName(source.CustomerInfo.BillingAddress);
            contact.ElectronicMail = source.CustomerInfo.Email;
            contact.Telefax = source.CustomerInfo.Fax;
            contact.Telephone = source.CustomerInfo.Phone;
            contact.ID = source.CustomerInfo.CustomerId;
            var nickName = Sitecore.Context.Domain.GetShortName(source.CustomerInfo.NickName);
            if (!string.IsNullOrEmpty(nickName) && nickName != Sitecore.Constants.AnonymousUserName)
            {
                contact.UserName = source.CustomerInfo.NickName;
            }

            party.Contact = contact;
            party.PostalAddress = GetAddress(source.CustomerInfo.BillingAddress);
            party.PartyName = GetFullName(source.CustomerInfo.BillingAddress);
            party.LanguageCode = Sitecore.Context.Language.Name;
            party.Person = GetPerson(source.CustomerInfo.BillingAddress);

            customerParty.Party = party;
            customerParty.SupplierAssignedAccountID = source.CustomerInfo.CustomerId;

            return customerParty;
        }

        protected virtual string GetFullName(AddressInfo source)
        {
            return string.Format("{0} {1}", source.Name, source.Name2);
        }

        protected virtual Address GetAddress(AddressInfo source)
        {
            Assert.ArgumentNotNull(source, "source");

            var address = OrderFactory.CreateAddress();

            address.AddressLine = source.Address;
            address.AddressLine2 = source.Address2;
            address.PostalZone = source.Zip;
            address.CityName = source.City;
            address.CountrySubentity = source.State;
            address.CountrySubentityCode = source.State;
            address.Country = source.Country != null ? source.Country.Title : string.Empty;
            address.CountryCode = source.Country != null ? source.Country.Code : string.Empty;
            address.AddressTypeCode = string.Empty;

            return address;
        }

        protected virtual Person GetPerson(AddressInfo source)
        {
            Assert.ArgumentNotNull(source, "source");

            var person = OrderFactory.CreatePerson();

            person.FirstName = source.Name;
            person.FamilyName = source.Name2;

            return person;
        }

        protected virtual ICollection<Sitecore.Ecommerce.OrderManagement.Orders.Delivery> GetDelivery(ActiveCommerce.Orders.Legacy.Order source, Order order)
        {
            Assert.ArgumentNotNull(source, "source");
            Assert.ArgumentNotNull(order, "order");
            Assert.IsNotNull(source.CustomerInfo, "customerinfo");

            var delivery = OrderFactory.CreateDelivery();
            var party = OrderFactory.CreateParty();
            var contact = OrderFactory.CreateContact();

            contact.Name = GetFullName(source.CustomerInfo.ShippingAddress);
            contact.Telephone = source.CustomerInfo.ShippingAddress.GetPhoneNumber();

            party.Contact = contact;
            party.PostalAddress = GetAddress(source.CustomerInfo.ShippingAddress);
            party.Person = GetPerson(source.CustomerInfo.ShippingAddress);
            party.PartyName = GetFullName(source.CustomerInfo.ShippingAddress);

            delivery.Order = order;
            delivery.DeliveryParty = party;

            return new Collection<Sitecore.Ecommerce.OrderManagement.Orders.Delivery> { delivery };
        }

        protected virtual ICollection<Sitecore.Ecommerce.Common.Party> GetFreightForwarderParty(ActiveCommerce.Orders.Legacy.Order source)
        {
            Assert.ArgumentNotNull(source, "source");

            var party = OrderFactory.CreateParty();
            party.PostalAddress = OrderFactory.CreateAddress();

            party.Person = OrderFactory.CreatePerson();
            party.PartyIdentification = source.ShippingProvider != null ? source.ShippingProvider.Code : null;
            party.PartyName = source.ShippingProvider != null ? source.ShippingProvider.Title : null;

            var acShippingProvider = (source.ShippingProvider as ActiveCommerce.Shipping.ShippingProvider);
            party.EndpointID = acShippingProvider != null ? acShippingProvider.ServiceCode : string.Empty;
            
            return new Collection<Sitecore.Ecommerce.Common.Party> { party };
        }

        protected virtual OrderLine GetOrderLine(ActiveCommerce.Orders.Legacy.OrderLine cartLine, string currencyCode, string discountCode)
        {
            Assert.ArgumentNotNull(cartLine, "cartLine");
            Assert.ArgumentNotNullOrEmpty(currencyCode, "currencyCode");

            var orderLine = OrderFactory.CreateOrderLine();
            var lineItem = OrderFactory.CreateLineItem();
            var item = OrderFactory.CreateItem();

            item.Code = cartLine.Product.Code;
            item.Sku = cartLine.Product.SKU;
            item.Name = cartLine.Product.Title;
            item.AdditionalInformation = cartLine.Details;
            item.Type = cartLine.Type;

            lineItem.Item = item;
            lineItem.Price = OrderFactory.CreatePrice(OrderFactory.CreateAmount(cartLine.Totals.PriceExVat, currencyCode), cartLine.Quantity);
            lineItem.TotalTaxAmount = OrderFactory.CreateAmount(cartLine.Totals.TotalVat, currencyCode);
            lineItem.LineExtensionAmount = OrderFactory.CreateAmount(cartLine.Totals.TotalPriceExVat, currencyCode);
            lineItem.Quantity = cartLine.Quantity;
            orderLine.LineItem = lineItem;

            if (cartLine.Totals.DiscountIncVat > 0)
            {
                orderLine.AllowanceCharge.Add(GetAllowanceCharge(cartLine.Totals.DiscountExVat, discountCode, currencyCode));
            }

            return orderLine;
        }

    }
}