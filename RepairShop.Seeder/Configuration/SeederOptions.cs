namespace RepairShop.Seeder.Configuration;

internal sealed class SeederOptions
{
    public DataverseOptions Dataverse { get; set; } = new();

    public DefaultSeedOptions Defaults { get; set; } = new();

    public SchemaOptions Schema { get; set; } = new();
}

internal sealed class DataverseOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

internal sealed class DefaultSeedOptions
{
    public int ContactCount { get; set; } = 25;

    public int CaseCount { get; set; } = 50;

    public int InventoryCount { get; set; } = 24;

    public int? RandomSeed { get; set; }
}
