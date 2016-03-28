using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using ActiveCommerce.Extensions;

namespace ActiveCommerce.Migration.CustomerAddresses.Migration
{
    public partial class CustomerAddresses : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
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

        protected void btnMigrate_Click(object sender, EventArgs e)
        {
            var assistant = new MigrationAssistant();

            var results = assistant.Process(false);

            this.ltlResults.Text = string.Format("Migrated {0} addresses for {1} users", results.AddressesUpdated, results.UsersUpdated);
        }

        protected void btnTest_Click(object sender, EventArgs e)
        {
            var assistant = new MigrationAssistant();

            var results = assistant.Process(true);

            this.ltlResults.Text = string.Format("Found {0} addresses for {1} users", results.AddressesUpdated, results.UsersUpdated);
        }
    }
}