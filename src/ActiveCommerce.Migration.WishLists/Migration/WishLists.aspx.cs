﻿using System;
using ActiveCommerce.Extensions;
using ActiveCommerce.ShopContext;
using Sitecore.Sites;
using Sitecore.Diagnostics;

namespace ActiveCommerce.Migration.WishLists.Migration
{
    public partial class WishLists : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            ltlResults.Text = string.Empty;

            if (IsPostBack)
            {
                return;
            }
            var sites = Sitecore.Sites.SiteManager.GetSites().EcommerceOnly();
            ddlSites.DataSource = sites;
            ddlSites.DataTextField = "Name";
            ddlSites.DataValueField = "Name";
            ddlSites.DataBind();
        }

        protected virtual SiteContext GetSiteContext()
        {
            if (string.IsNullOrEmpty(ddlSites.SelectedValue))
            {
                throw new ArgumentException("Please select a site.");
            }
            var siteContext = SiteContextFactory.GetSiteContext(ddlSites.SelectedValue);
            if (siteContext == null)
            {
                throw new Exception("Failed to get site context for selected site.");
            }
            return siteContext;
        }

        protected virtual void btnMigrate_Click(object sender, EventArgs e)
        {
            try
            {
                Assert.IsNotNullOrEmpty(txtFolderId.Text, "Please enter the Wish List Folder ID");
                int totalMigrated;
                using (new ShopContextSwitcher(GetSiteContext()))
                {
                    var assistant = new MigrationAssistant(Sitecore.Data.ID.Parse(txtFolderId.Text));
                    totalMigrated = assistant.Process();
                }
                ltlResults.Text = string.Format("Success! Migrated {0} wish lists.", totalMigrated);
            }
            catch (Exception ex)
            {
                ltlResults.Text = string.Format("Error migrating wish lists: {0}\n\r{1}", ex.Message, ex);
            }
        }
    }
}