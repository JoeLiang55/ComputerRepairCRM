namespace RepairShop.Seeder.Infrastructure;

internal static class SeedTag
{
    public const string Prefix = "RepairShop.Seeder:";

    public static string ForBatch(Guid batchId) => Prefix + batchId.ToString("D");
}
