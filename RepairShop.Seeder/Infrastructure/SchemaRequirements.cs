using RepairShop.Seeder.CommandLine;
using RepairShop.Seeder.Configuration;
using Microsoft.Xrm.Sdk.Metadata;

namespace RepairShop.Seeder.Infrastructure;

internal sealed record TableRequirement(
    string DisplayName,
    string Entity,
    string SeedBatchAttribute,
    IReadOnlyCollection<string> Attributes);

internal sealed record LookupRequirement(
    string DisplayName,
    string Entity,
    string Attribute,
    string ExpectedTarget);

internal sealed record AttributeTypeRequirement(
    string DisplayName,
    string Entity,
    string Attribute,
    IReadOnlyCollection<AttributeTypeCode> AllowedTypes);

internal sealed record SchemaRequirementSet(
    IReadOnlyCollection<TableRequirement> Tables,
    IReadOnlyCollection<LookupRequirement> Lookups,
    IReadOnlyCollection<AttributeTypeRequirement> AttributeTypes);

internal static class SchemaRequirements
{
    public static SchemaRequirementSet ForCommand(SeederCommand command, SchemaOptions schema)
    {
        bool needsContacts = command.SeedAll || command.Clear || command.ContactCount.HasValue ||
            command.CaseCount.HasValue;
        bool needsCases = command.SeedAll || command.Clear || command.CaseCount.HasValue;
        bool needsInventory = command.SeedAll || command.Clear || command.Inventory;
        bool needsRepairParts = command.SeedAll || command.Clear ||
            (command.CaseCount.HasValue && command.Inventory);

        var tables = new List<TableRequirement>();
        var lookups = new List<LookupRequirement>();
        var attributeTypes = new List<AttributeTypeRequirement>();

        if (needsContacts)
        {
            tables.Add(new TableRequirement(
                "Contact",
                schema.Contact.Entity,
                schema.Contact.SeedBatch,
                [schema.Contact.FirstName, schema.Contact.LastName, schema.Contact.Email, schema.Contact.Phone]));
            AddTypes(
                attributeTypes,
                "Contact",
                schema.Contact.Entity,
                AttributeTypeCode.String,
                schema.Contact.FirstName,
                schema.Contact.LastName,
                schema.Contact.Email,
                schema.Contact.Phone);
        }

        if (needsCases)
        {
            tables.Add(new TableRequirement(
                "Device",
                schema.Device.Entity,
                schema.Device.SeedBatch,
                [
                    schema.Device.Name,
                    schema.Device.Manufacturer,
                    schema.Device.Model,
                    schema.Device.SerialNumber,
                    schema.Device.WarrantyExpiry,
                    schema.Device.ContactLookup
                ]));
            tables.Add(new TableRequirement(
                "Case",
                schema.Case.Entity,
                schema.Case.SeedBatch,
                [
                    schema.Case.Title,
                    schema.Case.CustomerLookup,
                    schema.Case.DeviceLookup,
                    schema.Case.Priority,
                    schema.Case.RepairStatus,
                    schema.Case.DateReceived,
                    schema.Case.EstimatedCost,
                    schema.Case.FinalCost,
                    schema.Case.CompletionDate,
                    schema.Case.RepairDuration
                ]));
            lookups.Add(new LookupRequirement(
                "Device.Contact",
                schema.Device.Entity,
                schema.Device.ContactLookup,
                schema.Contact.Entity));
            lookups.Add(new LookupRequirement(
                "Case.Device",
                schema.Case.Entity,
                schema.Case.DeviceLookup,
                schema.Device.Entity));
            lookups.Add(new LookupRequirement(
                "Case.Customer",
                schema.Case.Entity,
                schema.Case.CustomerLookup,
                schema.Contact.Entity));
            AddTypes(attributeTypes, "Device text", schema.Device.Entity, AttributeTypeCode.String,
                schema.Device.Name, schema.Device.Manufacturer, schema.Device.Model, schema.Device.SerialNumber);
            AddTypes(attributeTypes, "Device date", schema.Device.Entity, AttributeTypeCode.DateTime,
                schema.Device.WarrantyExpiry);
            AddTypes(attributeTypes, "Device lookup", schema.Device.Entity, AttributeTypeCode.Lookup,
                schema.Device.ContactLookup);
            AddTypes(attributeTypes, "Case text", schema.Case.Entity, AttributeTypeCode.String,
                schema.Case.Title);
            attributeTypes.Add(new AttributeTypeRequirement(
                "Case customer lookup",
                schema.Case.Entity,
                schema.Case.CustomerLookup,
                [AttributeTypeCode.Customer, AttributeTypeCode.Lookup]));
            AddTypes(attributeTypes, "Case lookup", schema.Case.Entity, AttributeTypeCode.Lookup,
                schema.Case.DeviceLookup);
            AddTypes(attributeTypes, "Case Choice", schema.Case.Entity, AttributeTypeCode.Picklist,
                schema.Case.Priority, schema.Case.RepairStatus);
            AddTypes(attributeTypes, "Case date", schema.Case.Entity, AttributeTypeCode.DateTime,
                schema.Case.DateReceived, schema.Case.CompletionDate);
            AddTypes(attributeTypes, "Case currency", schema.Case.Entity, AttributeTypeCode.Money,
                schema.Case.EstimatedCost, schema.Case.FinalCost);
            AddTypes(attributeTypes, "Case duration", schema.Case.Entity, AttributeTypeCode.Integer,
                schema.Case.RepairDuration);
        }

        if (needsInventory)
        {
            tables.Add(new TableRequirement(
                "Inventory",
                schema.Inventory.Entity,
                schema.Inventory.SeedBatch,
                [
                    schema.Inventory.Name,
                    schema.Inventory.Sku,
                    schema.Inventory.StockQuantity,
                    schema.Inventory.UnitCost
                ]));
            AddTypes(attributeTypes, "Inventory text", schema.Inventory.Entity, AttributeTypeCode.String,
                schema.Inventory.Name, schema.Inventory.Sku);
            AddTypes(attributeTypes, "Inventory quantity", schema.Inventory.Entity, AttributeTypeCode.Integer,
                schema.Inventory.StockQuantity);
            AddTypes(attributeTypes, "Inventory currency", schema.Inventory.Entity, AttributeTypeCode.Money,
                schema.Inventory.UnitCost);
        }

        if (needsRepairParts)
        {
            tables.Add(new TableRequirement(
                "Repair Part",
                schema.RepairPart.Entity,
                schema.RepairPart.SeedBatch,
                [
                    schema.RepairPart.Name,
                    schema.RepairPart.CaseLookup,
                    schema.RepairPart.InventoryLookup,
                    schema.RepairPart.Quantity
                ]));
            lookups.Add(new LookupRequirement(
                "Repair Part.Case",
                schema.RepairPart.Entity,
                schema.RepairPart.CaseLookup,
                schema.Case.Entity));
            lookups.Add(new LookupRequirement(
                "Repair Part.Inventory",
                schema.RepairPart.Entity,
                schema.RepairPart.InventoryLookup,
                schema.Inventory.Entity));
            AddTypes(attributeTypes, "Repair Part text", schema.RepairPart.Entity, AttributeTypeCode.String,
                schema.RepairPart.Name);
            AddTypes(attributeTypes, "Repair Part lookup", schema.RepairPart.Entity, AttributeTypeCode.Lookup,
                schema.RepairPart.CaseLookup, schema.RepairPart.InventoryLookup);
            AddTypes(attributeTypes, "Repair Part quantity", schema.RepairPart.Entity, AttributeTypeCode.Integer,
                schema.RepairPart.Quantity);
        }

        return new SchemaRequirementSet(tables, lookups, attributeTypes);
    }

    private static void AddTypes(
        ICollection<AttributeTypeRequirement> requirements,
        string displayName,
        string entity,
        AttributeTypeCode allowedType,
        params string[] attributes)
    {
        foreach (string attribute in attributes)
        {
            requirements.Add(new AttributeTypeRequirement(
                displayName,
                entity,
                attribute,
                [allowedType]));
        }
    }
}
