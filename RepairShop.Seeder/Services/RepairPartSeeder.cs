using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Models;
using RepairShop.Seeder.Infrastructure;
using RepairShop.Seeder.Schema;

namespace RepairShop.Seeder.Services;

internal sealed class RepairPartSeeder(
    IOrganizationService service,
    string seedBatchAttribute,
    Random random)
{
    public int Seed(
        IReadOnlyList<SeededCase> cases,
        IReadOnlyList<SeededInventoryItem> inventory,
        string seedTag)
    {
        if (inventory.Count == 0)
        {
            return 0;
        }

        int created = 0;
        foreach (SeededCase repairCase in cases)
        {
            int partCount = random.Next(1, Math.Min(4, inventory.Count + 1));
            foreach (SeededInventoryItem item in inventory
                         .OrderBy(_ => random.Next())
                         .Take(partCount))
            {
                int quantity = random.Next(1, 3);
                string name = $"{item.Name} for {repairCase.Title}";
                if (name.Length > 95)
                {
                    name = name[..95];
                }

                var repairPart = new Entity(DataverseSchema.RepairPart.Entity)
                {
                    [DataverseSchema.RepairPart.Name] = name,
                    [DataverseSchema.RepairPart.Case] = new EntityReference(
                        DataverseSchema.Incident.Entity,
                        repairCase.Id),
                    [DataverseSchema.RepairPart.Inventory] = new EntityReference(
                        DataverseSchema.Inventory.Entity,
                        item.Id),
                    [DataverseSchema.RepairPart.Quantity] = quantity,
                    [DataverseSchema.RepairPart.UnitCost] = new Money(item.UnitCost),
                    [DataverseSchema.RepairPart.TotalCost] = new Money(item.UnitCost * quantity)
                };
                SeedTag.Apply(repairPart, seedBatchAttribute, seedTag);
                service.Create(repairPart);
                created++;
            }
        }

        return created;
    }
}
