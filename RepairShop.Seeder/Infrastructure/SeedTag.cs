using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Configuration;

namespace RepairShop.Seeder.Infrastructure;

internal static class SeedTag
{
    public const string Prefix = "RepairShop.Seeder:";

    public static string ForBatch(Guid batchId) => Prefix + batchId.ToString("D");

    public static bool IsCleanupAvailable(SchemaOptions schema) =>
        IsConfigured(schema.ContactSeedBatch) &&
        IsConfigured(schema.CaseSeedBatch) &&
        IsConfigured(schema.DeviceSeedBatch) &&
        IsConfigured(schema.InventorySeedBatch) &&
        IsConfigured(schema.RepairPartSeedBatch);

    public static void Apply(Entity entity, string attributeName, string value)
    {
        if (IsConfigured(attributeName))
        {
            entity[attributeName] = value;
        }
    }

    private static bool IsConfigured(string value) => !string.IsNullOrWhiteSpace(value);
}
