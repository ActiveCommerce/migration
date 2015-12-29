using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using ActiveCommerce.Extensions;
using ActiveCommerce.ShopContext;
using Sitecore.Diagnostics;
using Sitecore.Sites;

namespace ActiveCommerce.Migration.Orders.Migration
{
    public partial class Orders : System.Web.UI.Page
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
                int totalMigrated;
                using (new ShopContextSwitcher(GetSiteContext()))
                {
                    var assistant = new MigrationAssistant(testOnly: false);
                    totalMigrated = assistant.Process();
                }
                ltlResults.Text = string.Format("Success! Migrated {0} orders.", totalMigrated);
            }
            catch (Exception ex)
            {
                ltlResults.Text = string.Format("Error migrating orders: {0}\n\r{1}", ex.Message, ex);
            }
        }

        protected virtual void btnTest_Click(object sender, EventArgs e)
        {
            try
            {
                int totalMigrated;
                using (new ShopContextSwitcher(GetSiteContext()))
                {
                    var assistant = new MigrationAssistant(testOnly: true);
                    totalMigrated = assistant.Process();
                }
                ltlResults.Text = string.Format("Would have migrated {0} orders.", totalMigrated);
            }
            catch (Exception ex)
            {
                ltlResults.Text = string.Format("Error testing migrating orders: {0}\n\r{1}", ex.Message, ex);
            }
        }
    }
}