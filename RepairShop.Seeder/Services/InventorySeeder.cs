using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Data;
using RepairShop.Seeder.Infrastructure;
using RepairShop.Seeder.Models;
using RepairShop.Seeder.Schema;

namespace RepairShop.Seeder.Services;

internal sealed class InventorySeeder(
    IOrganizationService service,
    string seedBatchAttribute,
    Random random)
{
    public IReadOnlyList<SeededInventoryItem> Seed(int count, string seedTag)
    {
        var inventory = new List<SeededInventoryItem>(count);

        for (int index = 0; index < count; index++)
        {
            InventoryTemplate template = RealisticData.Inventory[index % RealisticData.Inventory.Length];
            string variant = random.Pick(new[] { "Standard", "Premium", "OEM", "Compatible" });
            string name = $"{template.Name} - {variant}";
            decimal unitCost = random.Money(template.MinimumCost, template.MaximumCost);
            int reorderLevel = random.Next(2, 11);
            var item = new Entity(DataverseSchema.Inventory.Entity)
            {
                [DataverseSchema.Inventory.Name] = name,
                [DataverseSchema.Inventory.Sku] = $"{template.Name.Replace(" ", string.Empty).ToUpperInvariant()[..Math.Min(4, template.Name.Replace(" ", string.Empty).Length)]}-{random.AlphaNumeric(6)}",
                [DataverseSchema.Inventory.StockQuantity] = random.Next(reorderLevel, 41),
                [DataverseSchema.Inventory.UnitCost] = new Money(unitCost),
                [DataverseSchema.Inventory.ReorderLevel] = reorderLevel
            };
            SeedTag.Apply(item, seedBatchAttribute, seedTag);

            inventory.Add(new SeededInventoryItem(service.Create(item), name, unitCost));
        }

        return inventory;
    }
}
