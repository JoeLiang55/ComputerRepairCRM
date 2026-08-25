using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Infrastructure;
using RepairShop.Seeder.Schema;

namespace RepairShop.Seeder.Services;

internal sealed class DemoDataCleaner(IOrganizationService service, SchemaOptions schema)
{
    public int Clear()
    {
        int deleted = 0;

        // Child records are deleted before their parents to satisfy lookup relationships.
        deleted += DeleteTaggedRecords(
            "Repair Parts",
            DataverseSchema.RepairPart.Entity,
            schema.RepairPartSeedBatch);
        deleted += DeleteTaggedRecords(
            "Cases",
            DataverseSchema.Incident.Entity,
            schema.CaseSeedBatch);
        deleted += DeleteTaggedRecords(
            "Devices",
            DataverseSchema.Device.Entity,
            schema.DeviceSeedBatch);
        deleted += DeleteTaggedRecords(
            "Inventory",
            DataverseSchema.Inventory.Entity,
            schema.InventorySeedBatch);
        deleted += DeleteTaggedRecords(
            "Contacts",
            DataverseSchema.Contact.Entity,
            schema.ContactSeedBatch);

        return deleted;
    }

    private int DeleteTaggedRecords(
        string displayName,
        string entityLogicalName,
        string seedBatchAttribute)
    {
        IReadOnlyList<Guid> ids = RetrieveTaggedRecordIds(entityLogicalName, seedBatchAttribute);
        foreach (Guid id in ids)
        {
            service.Delete(entityLogicalName, id);
        }

        Console.WriteLine($"Deleted {displayName}: {ids.Count}");
        return ids.Count;
    }

    private IReadOnlyList<Guid> RetrieveTaggedRecordIds(
        string entityLogicalName,
        string seedBatchAttribute)
    {
        var ids = new List<Guid>();
        var query = new QueryExpression(entityLogicalName)
        {
            ColumnSet = new ColumnSet(false),
            PageInfo = new PagingInfo
            {
                Count = 5_000,
                PageNumber = 1
            }
        };
        query.Criteria.AddCondition(seedBatchAttribute, ConditionOperator.BeginsWith, SeedTag.Prefix);

        while (true)
        {
            EntityCollection page = service.RetrieveMultiple(query);
            ids.AddRange(page.Entities.Select(entity => entity.Id));
            if (!page.MoreRecords)
            {
                break;
            }

            query.PageInfo.PageNumber++;
            query.PageInfo.PagingCookie = page.PagingCookie;
        }

        return ids;
    }
}
