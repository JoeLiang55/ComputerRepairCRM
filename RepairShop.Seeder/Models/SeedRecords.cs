namespace RepairShop.Seeder.Models;

internal sealed record SeededContact(Guid Id);

internal sealed record SeededDevice(Guid Id, Guid ContactId, string DisplayName);

internal sealed record SeededInventoryItem(Guid Id, string Name, decimal UnitCost);

internal sealed record SeededCase(Guid Id, string Title);

internal sealed record SeedRunResult(
    Guid BatchId,
    int Contacts,
    int Devices,
    int Cases,
    int InventoryItems,
    int RepairParts);
