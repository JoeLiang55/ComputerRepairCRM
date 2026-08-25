using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Data;
using RepairShop.Seeder.Models;

namespace RepairShop.Seeder.Services;

internal sealed class InventorySeeder(
    IOrganizationService service,
    InventorySchema schema,
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
            var item = new Entity(schema.Entity)
            {
                [schema.Name] = name,
                [schema.Sku] = $"{template.Name.Replace(" ", string.Empty).ToUpperInvariant()[..Math.Min(4, template.Name.Replace(" ", string.Empty).Length)]}-{random.AlphaNumeric(6)}",
                [schema.StockQuantity] = random.Next(2, 41),
                [schema.UnitCost] = new Money(random.Money(template.MinimumCost, template.MaximumCost)),
                [schema.SeedBatch] = seedTag
            };

            inventory.Add(new SeededInventoryItem(service.Create(item), name));
        }

        return inventory;
    }
}
