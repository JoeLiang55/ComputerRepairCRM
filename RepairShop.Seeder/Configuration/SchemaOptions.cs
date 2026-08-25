namespace RepairShop.Seeder.Configuration;

internal sealed class SchemaOptions
{
    public ContactSchema Contact { get; set; } = new();

    public DeviceSchema Device { get; set; } = new();

    public CaseSchema Case { get; set; } = new();

    public InventorySchema Inventory { get; set; } = new();

    public RepairPartSchema RepairPart { get; set; } = new();
}

internal abstract class SeedableSchema
{
    public string Entity { get; set; } = string.Empty;

    public string SeedBatch { get; set; } = string.Empty;
}

internal sealed class ContactSchema : SeedableSchema
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

internal sealed class DeviceSchema : SeedableSchema
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string WarrantyExpiry { get; set; } = string.Empty;
    public string ContactLookup { get; set; } = string.Empty;
}

internal sealed class CaseSchema : SeedableSchema
{
    public string Title { get; set; } = string.Empty;
    public string CustomerLookup { get; set; } = string.Empty;
    public string DeviceLookup { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string RepairStatus { get; set; } = string.Empty;
    public string DateReceived { get; set; } = string.Empty;
    public string EstimatedCost { get; set; } = string.Empty;
    public string FinalCost { get; set; } = string.Empty;
    public string CompletionDate { get; set; } = string.Empty;
    public string RepairDuration { get; set; } = string.Empty;
}

internal sealed class InventorySchema : SeedableSchema
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string StockQuantity { get; set; } = string.Empty;
    public string UnitCost { get; set; } = string.Empty;
}

internal sealed class RepairPartSchema : SeedableSchema
{
    public string Name { get; set; } = string.Empty;
    public string CaseLookup { get; set; } = string.Empty;
    public string InventoryLookup { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
}
