Active Commerce Data Migration
==============================
This repository contains example code to guide you in migrating data from Active Commerce 3.1 to Active Commerce 3.2. As every implementation is different, especially with regard to customizations to stored data, we strongly recommend that you thoroughly test the included code and customize to your needs.

Building
----
* If you are converting order data, install the appropriate version of the *Active Commerce Legacy Orders* plugin on your upgraded Active Commerce 3.2 instance.
  * This plugin is included in the download package for your Active Commerce version.
* Pull down this repository locally.
* Edit *src/deploy.targets* and *src/TdsGlobal.config* for your Active Commerce 3.2 Sitecore instance details.
  * If you don't have TDS, or if you wish to run on a non-local Sitecore instance, you can use another mechanism to deploy build files (e.g. a Visual Studio *Publish*).
  * The path in the *deploy.targets* file is used for assembly references in the projects. Alternatively you could update the references to a different source of assembly DLLs for Active Commerce, SES, Sitecore, etc. 
* Open the solution in Visual Studio, customize to your needs, build, and test.

Wish List Data (AC 3.1 or earlier to AC 3.2+)
----
Wish List data conversion reads wish lists from the Sitecore content tree by searching for them in the *sitecore_master_index* at a specified location (i.e. the wish lists root in the content tree). To test wish list conversion:
* Build the *ActiveCommerce.Migration.WishLists* project
* Open */Migration/WishLists.aspx* in your browser
* Enter the Wish List Folder ID (the root folder of Wish List items for your site) and select a destination Active Commerce site
* Click *Migrate*

Order Data (AC 3.1 or earlier to AC 3.2+)
----
Order data conversion reads orders from the Sitecore content tree using the legacy orders API, maps data appropriately to the new
Order domain model, and saves the historical order to the database utilizing the new order repository API. To test order data conversion:
* Ensure you have installed the *Legacy Orders* plugin, as described above.
  * You do not need to enable the plugin as described in the *Configuration Guide*. It is enough to simply install it.
* Review the TODO items in the *MigrationAssistant* class of the *ActiveCommerce.Migration.Orders* project.
  * Add logic to map any additional order or order line data which you are capturing
  * Add additional mappings between legacy *Order Status* values to new *Order State* values
* Build the *ActiveCommerce.Migration.Orders* project
* Open */Migration/Orders.aspx* in your browser
* Select the Active Commerce site for which you want to convert order data
* Use the *Test Migration* button to test the data conversion
  * This will allow you to confirm the number of orders that will be migrated, and that no errors occur during the data mapping process
* Use *Migrate* to perform the data conversion and save the historical orders to the orders database

Customer Addresses (AC 3.2 or earlier to AC 3.3)
----
Customer address conversion reads the saved shipping/billing addresses from the customer's ASP.NET Membership Profile, maps data into
the Address domain model, and saves the addresses to the customer's address book in the Active Commerce database. To test order data conversion:
* Review the TODO items in the *MigrationAssistant* class of the *ActiveCommerce.Migration.CustomerAddresses* project.
  * Add logic to instantiate your extended address type, if applicable, and map/populate any additional fields you've added to it.
* Build the *ActiveCommerce.Migration.CustomerAddresses* project
* Open */Migration/CustomerAddresses.aspx* in your browser
* Use the *Test Migration* button to test the data conversion
  * This will allow you to confirm the number of addresses that will be migrated, and that no errors occur during the data mapping process
* Use *Migrate* to perform the data conversion and save the customer addresses to the Active Commerce database