namespace RepairShop.Seeder.CommandLine;

internal sealed record SeederCommand(
    bool SeedAll,
    bool Clear,
    int? ContactCount,
    int? CaseCount,
    bool Inventory,
    bool ShowHelp);
