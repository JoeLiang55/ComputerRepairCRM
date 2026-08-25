namespace RepairShop.Seeder.Schema;

internal static class DataverseSchema
{
    internal static class Contact
    {
        internal const string Entity = "contact";
        internal const string FirstName = "firstname";
        internal const string LastName = "lastname";
        internal const string Email = "emailaddress1";
        internal const string Phone = "telephone1";
    }

    internal static class Incident
    {
        internal const string Entity = "incident";
        internal const string Title = "title";
        internal const string Customer = "customerid";
        internal const string Priority = "prioritycode";
        internal const string Device = "gsic_device";

        // Confirmed by the existing plug-in implementation and repository documentation.
        internal const string RepairStatus = "cr1a3_repairstatus";
        internal const string DateReceived = "cr1a3_datereceived";
        internal const string EstimatedCost = "cr1a3_estimatedcost";
        internal const string FinalCost = "cr1a3_finalcost";
        internal const string CompletionDate = "gsic_completiondate";
        internal const string RepairDuration = "gsic_repairduration";
    }

    internal static class Device
    {
        internal const string Entity = "gsic_device";
        internal const string Name = "gsic_devicename";
        internal const string Manufacturer = "gsic_manufacturer";
        internal const string Model = "gsic_model";
        internal const string SerialNumber = "gsic_serialnumber";
        internal const string WarrantyExpiry = "gsic_warrantyexpiry";
        internal const string PurchaseDate = "gsic_purchasedate";
        internal const string Customer = "gsic_customer";
    }

    internal static class Inventory
    {
        internal const string Entity = "gsic_inventory";
        internal const string Name = "gsic_partname";
        internal const string Sku = "gsic_sku";
        internal const string StockQuantity = "gsic_stockquantity";
        internal const string UnitCost = "gsic_unitcost";
        internal const string ReorderLevel = "gsic_reorderlevel";
    }

    internal static class RepairPart
    {
        internal const string Entity = "gsic_repairpart";
        internal const string Name = "gsic_repairpartname";
        internal const string Case = "gsic_case";
        internal const string Inventory = "gsic_inventoryitem";
        internal const string Quantity = "gsic_quantity";
        internal const string UnitCost = "gsic_unitcost";
        internal const string TotalCost = "gsic_totalcost";
    }
}
