using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Data;
using RepairShop.Seeder.Models;

namespace RepairShop.Seeder.Services;

internal sealed class CaseSeeder(
    IOrganizationService service,
    CaseSchema schema,
    ContactSchema contactSchema,
    DeviceSchema deviceSchema,
    Random random)
{
    private const int Received = 702670000;
    private const int Diagnosing = 702670001;
    private const int WaitingForApproval = 702670002;
    private const int RepairInProgress = 702670003;
    private const int ReadyForPickup = 702670005;
    private const int Completed = 702670006;

    private static readonly int[] RepairStatuses =
    [
        Received,
        Received,
        Diagnosing,
        Diagnosing,
        WaitingForApproval,
        RepairInProgress,
        RepairInProgress,
        ReadyForPickup,
        Completed
    ];

    private static readonly string[] Problems =
    [
        "will not power on",
        "runs slowly and freezes",
        "has a cracked display",
        "does not charge reliably",
        "overheats under load",
        "has intermittent keyboard input",
        "cannot detect the storage drive",
        "shuts down unexpectedly"
    ];

    public IReadOnlyList<SeededCase> Seed(
        int count,
        IReadOnlyList<SeededDevice> devices,
        string seedTag)
    {
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("At least one Device is required to seed Cases.");
        }

        var cases = new List<SeededCase>(count);
        DateTime now = DateTime.UtcNow;

        for (int index = 0; index < count; index++)
        {
            SeededDevice device = random.Pick(devices);
            DateTime received = now.AddDays(-random.Next(0, 181)).AddMinutes(-random.Next(0, 1_440));
            decimal estimatedCost = random.Money(65m, 850m);
            int repairStatus = random.Pick(RepairStatuses);
            string title = $"{device.DisplayName} {random.Pick(Problems)}";

            var repairCase = new Entity(schema.Entity)
            {
                [schema.Title] = title,
                [schema.CustomerLookup] = new EntityReference(contactSchema.Entity, device.ContactId),
                [schema.DeviceLookup] = new EntityReference(deviceSchema.Entity, device.Id),
                [schema.Priority] = new OptionSetValue(random.Next(1, 4)),
                [schema.RepairStatus] = new OptionSetValue(repairStatus),
                [schema.DateReceived] = received,
                [schema.EstimatedCost] = new Money(estimatedCost),
                [schema.SeedBatch] = seedTag
            };

            if (repairStatus == Completed)
            {
                DateTime completion = received.AddDays(random.Next(1, 15));
                if (completion > now)
                {
                    completion = now;
                }

                repairCase[schema.FinalCost] = new Money(
                    decimal.Round(estimatedCost * random.Money(0.85m, 1.25m), 2));
                repairCase[schema.CompletionDate] = completion;
                repairCase[schema.RepairDuration] = Math.Max(
                    1,
                    checked((int)(completion - received).TotalMinutes));
            }

            cases.Add(new SeededCase(service.Create(repairCase), title));
        }

        return cases;
    }
}
