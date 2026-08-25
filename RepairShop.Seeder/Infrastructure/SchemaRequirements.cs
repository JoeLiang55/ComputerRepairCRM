using RepairShop.Seeder.CommandLine;
using RepairShop.Seeder.Configuration;
using Microsoft.Xrm.Sdk.Metadata;
using RepairShop.Seeder.Schema;

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
                DataverseSchema.Contact.Entity,
                schema.ContactSeedBatch,
                [
                    DataverseSchema.Contact.FirstName,
                    DataverseSchema.Contact.LastName,
                    DataverseSchema.Contact.Email,
                    DataverseSchema.Contact.Phone
                ]));
            AddTypes(
                attributeTypes,
                "Contact",
                DataverseSchema.Contact.Entity,
                AttributeTypeCode.String,
                DataverseSchema.Contact.FirstName,
                DataverseSchema.Contact.LastName,
                DataverseSchema.Contact.Email,
                DataverseSchema.Contact.Phone);
        }

        if (needsCases)
        {
            tables.Add(new TableRequirement(
                "Device",
                DataverseSchema.Device.Entity,
                schema.DeviceSeedBatch,
                [
                    DataverseSchema.Device.Name,
                    DataverseSchema.Device.Manufacturer,
                    DataverseSchema.Device.Model,
                    DataverseSchema.Device.SerialNumber,
                    DataverseSchema.Device.WarrantyExpiry,
                    DataverseSchema.Device.PurchaseDate,
                    DataverseSchema.Device.Customer
                ]));
            tables.Add(new TableRequirement(
                "Case",
                DataverseSchema.Incident.Entity,
                schema.CaseSeedBatch,
                [
                    DataverseSchema.Incident.Title,
                    DataverseSchema.Incident.Customer,
                    DataverseSchema.Incident.Device,
                    DataverseSchema.Incident.Priority,
                    DataverseSchema.Incident.RepairStatus,
                    DataverseSchema.Incident.DateReceived,
                    DataverseSchema.Incident.EstimatedCost,
                    DataverseSchema.Incident.FinalCost,
                    DataverseSchema.Incident.CompletionDate,
                    DataverseSchema.Incident.RepairDuration
                ]));
            lookups.Add(new LookupRequirement(
                "Device.Contact",
                DataverseSchema.Device.Entity,
                DataverseSchema.Device.Customer,
                DataverseSchema.Contact.Entity));
            lookups.Add(new LookupRequirement(
                "Case.Device",
                DataverseSchema.Incident.Entity,
                DataverseSchema.Incident.Device,
                DataverseSchema.Device.Entity));
            lookups.Add(new LookupRequirement(
                "Case.Customer",
                DataverseSchema.Incident.Entity,
                DataverseSchema.Incident.Customer,
                DataverseSchema.Contact.Entity));
            AddTypes(attributeTypes, "Device text", DataverseSchema.Device.Entity, AttributeTypeCode.String,
                DataverseSchema.Device.Name,
                DataverseSchema.Device.Manufacturer,
                DataverseSchema.Device.Model,
                DataverseSchema.Device.SerialNumber);
            AddTypes(attributeTypes, "Device date", DataverseSchema.Device.Entity, AttributeTypeCode.DateTime,
                DataverseSchema.Device.WarrantyExpiry, DataverseSchema.Device.PurchaseDate);
            AddTypes(attributeTypes, "Device lookup", DataverseSchema.Device.Entity, AttributeTypeCode.Lookup,
                DataverseSchema.Device.Customer);
            AddTypes(attributeTypes, "Case text", DataverseSchema.Incident.Entity, AttributeTypeCode.String,
                DataverseSchema.Incident.Title);
            attributeTypes.Add(new AttributeTypeRequirement(
                "Case customer lookup",
                DataverseSchema.Incident.Entity,
                DataverseSchema.Incident.Customer,
                [AttributeTypeCode.Customer, AttributeTypeCode.Lookup]));
            AddTypes(attributeTypes, "Case lookup", DataverseSchema.Incident.Entity, AttributeTypeCode.Lookup,
                DataverseSchema.Incident.Device);
            AddTypes(attributeTypes, "Case Choice", DataverseSchema.Incident.Entity, AttributeTypeCode.Picklist,
                DataverseSchema.Incident.Priority, DataverseSchema.Incident.RepairStatus);
            AddTypes(attributeTypes, "Case date", DataverseSchema.Incident.Entity, AttributeTypeCode.DateTime,
                DataverseSchema.Incident.DateReceived, DataverseSchema.Incident.CompletionDate);
            AddTypes(attributeTypes, "Case currency", DataverseSchema.Incident.Entity, AttributeTypeCode.Money,
                DataverseSchema.Incident.EstimatedCost, DataverseSchema.Incident.FinalCost);
            AddTypes(attributeTypes, "Case duration", DataverseSchema.Incident.Entity, AttributeTypeCode.Integer,
                DataverseSchema.Incident.RepairDuration);
        }

        if (needsInventory)
        {
            tables.Add(new TableRequirement(
                "Inventory",
                DataverseSchema.Inventory.Entity,
                schema.InventorySeedBatch,
                [
                    DataverseSchema.Inventory.Name,
                    DataverseSchema.Inventory.Sku,
                    DataverseSchema.Inventory.StockQuantity,
                    DataverseSchema.Inventory.UnitCost,
                    DataverseSchema.Inventory.ReorderLevel
                ]));
            AddTypes(attributeTypes, "Inventory text", DataverseSchema.Inventory.Entity, AttributeTypeCode.String,
                DataverseSchema.Inventory.Name, DataverseSchema.Inventory.Sku);
            AddTypes(attributeTypes, "Inventory quantity", DataverseSchema.Inventory.Entity, AttributeTypeCode.Integer,
                DataverseSchema.Inventory.StockQuantity, DataverseSchema.Inventory.ReorderLevel);
            AddTypes(attributeTypes, "Inventory currency", DataverseSchema.Inventory.Entity, AttributeTypeCode.Money,
                DataverseSchema.Inventory.UnitCost);
        }

        if (needsRepairParts)
        {
            tables.Add(new TableRequirement(
                "Repair Part",
                DataverseSchema.RepairPart.Entity,
                schema.RepairPartSeedBatch,
                [
                    DataverseSchema.RepairPart.Name,
                    DataverseSchema.RepairPart.Case,
                    DataverseSchema.RepairPart.Inventory,
                    DataverseSchema.RepairPart.Quantity,
                    DataverseSchema.RepairPart.UnitCost,
                    DataverseSchema.RepairPart.TotalCost
                ]));
            lookups.Add(new LookupRequirement(
                "Repair Part.Case",
                DataverseSchema.RepairPart.Entity,
                DataverseSchema.RepairPart.Case,
                DataverseSchema.Incident.Entity));
            lookups.Add(new LookupRequirement(
                "Repair Part.Inventory",
                DataverseSchema.RepairPart.Entity,
                DataverseSchema.RepairPart.Inventory,
                DataverseSchema.Inventory.Entity));
            AddTypes(attributeTypes, "Repair Part text", DataverseSchema.RepairPart.Entity, AttributeTypeCode.String,
                DataverseSchema.RepairPart.Name);
            AddTypes(attributeTypes, "Repair Part lookup", DataverseSchema.RepairPart.Entity, AttributeTypeCode.Lookup,
                DataverseSchema.RepairPart.Case, DataverseSchema.RepairPart.Inventory);
            AddTypes(attributeTypes, "Repair Part quantity", DataverseSchema.RepairPart.Entity, AttributeTypeCode.Integer,
                DataverseSchema.RepairPart.Quantity);
            AddTypes(attributeTypes, "Repair Part currency", DataverseSchema.RepairPart.Entity, AttributeTypeCode.Money,
                DataverseSchema.RepairPart.UnitCost, DataverseSchema.RepairPart.TotalCost);
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
