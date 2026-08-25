using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.CommandLine;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Infrastructure;
using RepairShop.Seeder.Models;

namespace RepairShop.Seeder.Services;

internal sealed class DemoDataSeeder(
    IOrganizationService service,
    SeederOptions options,
    Random random)
{
    public SeedRunResult Seed(SeederCommand command)
    {
        Guid batchId = Guid.NewGuid();
        string seedTag = SeedTag.ForBatch(batchId);
        int caseCount = command.SeedAll
            ? options.Defaults.CaseCount
            : command.CaseCount ?? 0;
        int contactCount = command.SeedAll
            ? options.Defaults.ContactCount
            : command.ContactCount ?? (caseCount > 0 ? Math.Max(1, (int)Math.Ceiling(caseCount / 3m)) : 0);
        int inventoryCount = command.SeedAll || command.Inventory
            ? options.Defaults.InventoryCount
            : 0;

        if (SeedTag.IsCleanupAvailable(options.Schema))
        {
            Console.WriteLine($"Seed batch: {batchId:D}");
            Console.WriteLine("The batch identifier is written to every seeded record.");
        }
        else
        {
            Console.WriteLine($"Seed run: {batchId:D}");
            Console.WriteLine("This seed run is not tagged for automated cleanup.");
        }

        var contactSeeder = new ContactSeeder(service, options.Schema.ContactSeedBatch, random);
        var deviceSeeder = new DeviceSeeder(
            service,
            options.Schema.DeviceSeedBatch,
            random);
        var caseSeeder = new CaseSeeder(
            service,
            options.Schema.CaseSeedBatch,
            random);
        var inventorySeeder = new InventorySeeder(
            service,
            options.Schema.InventorySeedBatch,
            random);
        var repairPartSeeder = new RepairPartSeeder(
            service,
            options.Schema.RepairPartSeedBatch,
            random);

        IReadOnlyList<SeededContact> contacts = contactCount > 0
            ? contactSeeder.Seed(contactCount, seedTag)
            : [];
        Console.WriteLine($"Created Contacts: {contacts.Count}");

        IReadOnlyList<SeededDevice> devices = caseCount > 0
            ? deviceSeeder.Seed(contacts, seedTag)
            : [];
        Console.WriteLine($"Created Devices: {devices.Count}");

        IReadOnlyList<SeededInventoryItem> inventory = inventoryCount > 0
            ? inventorySeeder.Seed(inventoryCount, seedTag)
            : [];
        Console.WriteLine($"Created Inventory items: {inventory.Count}");

        IReadOnlyList<SeededCase> cases = caseCount > 0
            ? caseSeeder.Seed(caseCount, devices, seedTag)
            : [];
        Console.WriteLine($"Created Cases: {cases.Count}");

        bool createRepairParts = cases.Count > 0 && inventory.Count > 0;
        int repairPartCount = createRepairParts
            ? repairPartSeeder.Seed(cases, inventory, seedTag)
            : 0;
        Console.WriteLine($"Created Repair Parts: {repairPartCount}");

        return new SeedRunResult(
            batchId,
            contacts.Count,
            devices.Count,
            cases.Count,
            inventory.Count,
            repairPartCount);
    }
}
