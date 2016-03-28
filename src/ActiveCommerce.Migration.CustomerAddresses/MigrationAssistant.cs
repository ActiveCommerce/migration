﻿using Sitecore.Ecommerce.DomainModel.Users;
using Sitecore.Security.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Ecommerce;
using ActiveCommerce.Addresses;
using Sitecore.Ecommerce.DomainModel.Data;

namespace ActiveCommerce.Migration.CustomerAddresses
{
    public class MigrationCounts
    {
        public int UsersUpdated { get; set; }

        public int AddressesUpdated { get; set; }
    }

    public class MigrationAssistant
    {
        private IEntityProvider<Sitecore.Ecommerce.DomainModel.Addresses.Country> countryProvider;

        public MigrationAssistant()
        {
            this.countryProvider = Sitecore.Ecommerce.Context.Entity.Resolve<IEntityProvider<Sitecore.Ecommerce.DomainModel.Addresses.Country>>();
        }

        public MigrationCounts Process(bool test)
        {
            MigrationCounts counts = new MigrationCounts();
            counts.AddressesUpdated = 0;
            counts.UsersUpdated = 0;

            var extranetUsers = UserManager.GetUsers()
                .Where(u => u.Domain.Name == "extranet");

            var addressRepository = Context.Entity.Resolve<IAddressRepository<AddressInfo>>();

            extranetUsers.ToList()
                .ForEach(u =>
                {
                    var customerId = u.Profile.GetCustomProperty("Customer ID");

                    if (!string.IsNullOrWhiteSpace(customerId))
                    {
                        var userAddresses = addressRepository.GetForCustomer(customerId);

                        if(!userAddresses.Any())
                        {
                            counts.UsersUpdated++;

                            var shippingAddress = this.GetShippingInfo(u.Profile);
                            shippingAddress.CustomerId = customerId;

                            var billingAddress = this.GetBillingInfo(u.Profile);
                            billingAddress.CustomerId = customerId;

                            if(shippingAddress.IsSameAsOtherAddress(billingAddress))
                            {
                                shippingAddress.IsBillingDefault = true;
                                addressRepository.Add(shippingAddress);

                                counts.AddressesUpdated++;
                            }
                            else
                            {
                                addressRepository.Add(shippingAddress);
                                addressRepository.Add(billingAddress);
                                counts.AddressesUpdated += 2;
                            }

                            if (!test)
                            {
                                addressRepository.Flush();
                            }
                        }
                    }
                });

            return counts;
        }

        private AddressInfo GetShippingInfo(Sitecore.Security.UserProfile profile)
        {
            AddressInfo shippingInfo = new AddressInfo();

            shippingInfo.Address = profile.GetCustomProperty("Shipping Address");
            shippingInfo.Address2 = profile.GetCustomProperty("Shipping Address 2");
            shippingInfo.City = profile.GetCustomProperty("Shipping Address City");

            var countryCode = profile.GetCustomProperty("Shipping Address Country Code");
            var country = this.countryProvider.GetDefault();

            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                country = this.countryProvider.Get(countryCode);
            }

            shippingInfo.Country = country;

            shippingInfo.Name = profile.GetCustomProperty("Shipping Address Name");
            shippingInfo.Name2 = profile.GetCustomProperty("Shipping Address Name 2");
            shippingInfo.Phone = profile.GetCustomProperty("Shipping Address Phone");
            shippingInfo.State = profile.GetCustomProperty("Shipping Address State");
            shippingInfo.Zip = profile.GetCustomProperty("Shipping Address Zip");

            shippingInfo.IsShippingDefault = true;

            return shippingInfo;
        }

        private AddressInfo GetBillingInfo(Sitecore.Security.UserProfile profile)
        {
            AddressInfo billingInfo = new AddressInfo();

            billingInfo.Address = profile.GetCustomProperty("Billing Address");
            billingInfo.Address2 = profile.GetCustomProperty("Billing Address 2");
            billingInfo.City = profile.GetCustomProperty("Billing Address City");

            var countryCode = profile.GetCustomProperty("Billing Address Country Code");
            var country = this.countryProvider.GetDefault();

            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                country = this.countryProvider.Get(countryCode);
            }
            
            billingInfo.Country = country;

            billingInfo.Name = profile.GetCustomProperty("Billing Address Name");
            billingInfo.Name2 = profile.GetCustomProperty("Billing Address Name 2");
            billingInfo.Phone = profile.GetCustomProperty("Billing Address Phone");
            billingInfo.State = profile.GetCustomProperty("Billing Address State");
            billingInfo.Zip = profile.GetCustomProperty("Billing Address Zip");

            billingInfo.IsBillingDefault = true;

            return billingInfo;
        }
    }
}