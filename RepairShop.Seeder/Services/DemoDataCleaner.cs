using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Infrastructure;

namespace RepairShop.Seeder.Services;

internal sealed class DemoDataCleaner(IOrganizationService service, SchemaOptions schema)
{
    public int Clear()
    {
        int deleted = 0;

        // Child records are deleted before their parents to satisfy lookup relationships.
        deleted += DeleteTaggedRecords("Repair Parts", schema.RepairPart);
        deleted += DeleteTaggedRecords("Cases", schema.Case);
        deleted += DeleteTaggedRecords("Devices", schema.Device);
        deleted += DeleteTaggedRecords("Inventory", schema.Inventory);
        deleted += DeleteTaggedRecords("Contacts", schema.Contact);

        return deleted;
    }

    private int DeleteTaggedRecords(string displayName, SeedableSchema table)
    {
        IReadOnlyList<Guid> ids = RetrieveTaggedRecordIds(table);
        foreach (Guid id in ids)
        {
            service.Delete(table.Entity, id);
        }

        Console.WriteLine($"Deleted {displayName}: {ids.Count}");
        return ids.Count;
    }

    private IReadOnlyList<Guid> RetrieveTaggedRecordIds(SeedableSchema table)
    {
        var ids = new List<Guid>();
        var query = new QueryExpression(table.Entity)
        {
            ColumnSet = new ColumnSet(false),
            PageInfo = new PagingInfo
            {
                Count = 5_000,
                PageNumber = 1
            }
        };
        query.Criteria.AddCondition(table.SeedBatch, ConditionOperator.BeginsWith, SeedTag.Prefix);

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
