using ActiveCommerce.Data;
using ActiveCommerce.Lists;
using Microsoft.Practices.Unity;
using Sitecore;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ActiveCommerce.Migration.WishLists
{
    public class MigrationAssistant
    {
        protected ID RepositoryRoot { get; private set; }

        public MigrationAssistant(ID repositoryRoot)
        {
            RepositoryRoot = repositoryRoot;
        }

        public int Process()
        {
            var legacyLists = GetLegacyWishLists();
            var sessionBuilder = Sitecore.Ecommerce.Context.Entity.Resolve<ISessionBuilder>();
            var session = sessionBuilder.GetSession();
            using (var transaction = session.BeginTransaction())
            {
                try
                {
                    foreach (var wishList in legacyLists)
                    {
                        session.Save(wishList);
                    }
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw e;
                }
            }
            return legacyLists.Count;
        }

        protected IList<IWishList> GetLegacyWishLists()
        {
            var lists = new List<IWishList>();

            using (var context = ContentSearchManager.GetIndex("sitecore_master_index").CreateSearchContext())
            {
                foreach (var listResultItem in context.GetQueryable<SearchResultItem>().Where(x => x.Paths.Contains(RepositoryRoot) && x.TemplateId == TemplateIDs.WishList))
                {
                    var list = GetWishList(listResultItem);
                    foreach (var lineResultItem in listResultItem.GetChildren<SearchResultItem>(context).Where(x => x.TemplateId == TemplateIDs.WishListLine))
                    {
                        var line = GetWishListLine(lineResultItem);
                        list.WishListLines.Add(line);
                    }
                    lists.Add(list);
                }
            }
            return lists;
        }

        protected IWishList GetWishList(SearchResultItem searchResultItem)
        {
            var item = searchResultItem.GetItem();

            return new WishList
                {
                    ID = searchResultItem.ItemId.ToGuid(),
                    CustomerId = ShortID.Decode(item["CustomerId"]).ToUpper(), // DeNormalize CustomerId
                    ShopContext = Sitecore.Context.Site.Name,
                    IsPublic = MainUtil.GetBool(item["IsPublic"], false),
                    LastModified = DateUtil.ParseDateTime(item["LastModified"], DateTime.Now),
                    Name = item["Name"],
                    WishListLines = new List<IWishListLine>()
                };
        }

        protected IWishListLine GetWishListLine(SearchResultItem searchResultItem)
        {
            var item = searchResultItem.GetItem();
            var productCode = item["ProductCode"];
            uint qty;

            return new WishListLine
                {
                    ProductCode = productCode,
                    Name = productCode,
                    Quantity = UInt32.TryParse(item["Quantity"], out qty) ? qty : 1
                };
        }
    }
}