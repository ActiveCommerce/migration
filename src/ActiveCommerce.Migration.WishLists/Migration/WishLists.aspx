<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="WishLists.aspx.cs" Inherits="ActiveCommerce.Migration.WishLists.Migration.WishLists" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Active Commerce - Wish List Migration</title>
    <style>
        label {
            display: block;
        }
        label > small {
            display: block;
            color: #999;
        }
        .field {
            display: block;
            margin: 10px 0;
        }
        .actions {
            border-top: 1px solid #ccc;
            border-bottom: 1px solid #ccc;
            margin: 10px 0;
            padding: 10px 0;
        }
        .results {
            color: #d56932;
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
    <div class="field">
        <label>Enter the source Wish List Folder ID:
            <small>This is typically found under your site's Webshop Business Settings (e.g. /sitecore/content/&lt;site&gt;/Business Catalog/Lists)</small>
        </label>
        <asp:TextBox runat="server" ID="txtFolderId" Width="300"></asp:TextBox>
    </div>
    <div class="field">
        <asp:Label runat="server" AssociatedControlID="ddlSites">Select the destination site:
            <small>The wish lists will be migrated to the database configured for this site ("ordersDatabase" attribute on the site definition)</small>
        </asp:Label>
        <asp:DropDownList runat="server" ID="ddlSites"/>
    </div>
    <div class="actions">
        <asp:Button runat="server" ID="btnMigrate" Text="Migrate" OnClick="btnMigrate_Click"/>
    </div>
    <div class="results">
<pre>
<asp:Literal runat="server" ID="ltlResults"></asp:Literal>
</pre>
    </div>
    </form>
</body>
</html>
