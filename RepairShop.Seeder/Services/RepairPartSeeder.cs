using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Models;

namespace RepairShop.Seeder.Services;

internal sealed class RepairPartSeeder(
    IOrganizationService service,
    RepairPartSchema schema,
    CaseSchema caseSchema,
    InventorySchema inventorySchema,
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
                string name = $"{item.Name} for {repairCase.Title}";
                if (name.Length > 95)
                {
                    name = name[..95];
                }

                var repairPart = new Entity(schema.Entity)
                {
                    [schema.Name] = name,
                    [schema.CaseLookup] = new EntityReference(caseSchema.Entity, repairCase.Id),
                    [schema.InventoryLookup] = new EntityReference(inventorySchema.Entity, item.Id),
                    [schema.Quantity] = random.Next(1, 3),
                    [schema.SeedBatch] = seedTag
                };
                service.Create(repairPart);
                created++;
            }
        }

        return created;
    }
}
