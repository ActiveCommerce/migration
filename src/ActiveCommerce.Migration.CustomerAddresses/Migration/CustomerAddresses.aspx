<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="CustomerAddresses.aspx.cs" Inherits="ActiveCommerce.Migration.CustomerAddresses.Migration.CustomerAddresses" %>

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
        <div class="actions">
            <asp:Button runat="server" ID="btnTest" Text="Test Migration" OnClick="btnTest_Click" />
            (Customer addresses won't be created)
        <br />
            <asp:Button runat="server" ID="btnMigrate" Text="Migrate" OnClick="btnMigrate_Click" />
        </div>
        <div class="results">
            <pre>
                <asp:Literal runat="server" ID="ltlResults"></asp:Literal>
            </pre>
        </div>
    </form>
</body>
</html>
