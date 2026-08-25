using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Messages;

namespace RepairShop.Seeder.Infrastructure;

internal sealed class SchemaValidator(IOrganizationService service)
{
    public void Validate(SchemaRequirementSet requirements)
    {
        var metadataByEntity = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);

        foreach (TableRequirement table in requirements.Tables)
        {
            ValidateConfiguredName(table.DisplayName + " table", table.Entity);
            ValidateConfiguredName(table.DisplayName + " seed batch column", table.SeedBatchAttribute);
            foreach (string attribute in table.Attributes)
            {
                ValidateConfiguredName(table.DisplayName + " column", attribute);
            }

            var response = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest
            {
                LogicalName = table.Entity,
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = true
            });
            metadataByEntity[table.Entity] = response.EntityMetadata;

            var availableAttributes = response.EntityMetadata.Attributes
                .Where(attribute => attribute.LogicalName is not null)
                .ToDictionary(attribute => attribute.LogicalName!, StringComparer.OrdinalIgnoreCase);

            foreach (string attributeName in table.Attributes.Append(table.SeedBatchAttribute))
            {
                if (!availableAttributes.ContainsKey(attributeName))
                {
                    throw new InvalidOperationException(
                        $"Configured {table.DisplayName} column '{table.Entity}.{attributeName}' does not exist.");
                }
            }

            AttributeMetadata seedAttribute = availableAttributes[table.SeedBatchAttribute];
            if (seedAttribute.AttributeType is not AttributeTypeCode.String and not AttributeTypeCode.Memo)
            {
                throw new InvalidOperationException(
                    $"Seed batch column '{table.Entity}.{table.SeedBatchAttribute}' must be a Text column.");
            }
        }

        foreach (LookupRequirement lookup in requirements.Lookups)
        {
            EntityMetadata entityMetadata = metadataByEntity[lookup.Entity];
            var lookupMetadata = entityMetadata.Attributes
                .OfType<LookupAttributeMetadata>()
                .SingleOrDefault(attribute => string.Equals(
                    attribute.LogicalName,
                    lookup.Attribute,
                    StringComparison.OrdinalIgnoreCase));

            if (lookupMetadata is null ||
                lookupMetadata.Targets is null ||
                !lookupMetadata.Targets.Contains(lookup.ExpectedTarget, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Configured lookup {lookup.DisplayName} ('{lookup.Entity}.{lookup.Attribute}') " +
                    $"does not target '{lookup.ExpectedTarget}'.");
            }
        }

        foreach (AttributeTypeRequirement typeRequirement in requirements.AttributeTypes)
        {
            AttributeMetadata metadata = metadataByEntity[typeRequirement.Entity].Attributes
                .Single(attribute => string.Equals(
                    attribute.LogicalName,
                    typeRequirement.Attribute,
                    StringComparison.OrdinalIgnoreCase));
            if (!metadata.AttributeType.HasValue ||
                !typeRequirement.AllowedTypes.Contains(metadata.AttributeType.Value))
            {
                string expected = string.Join(" or ", typeRequirement.AllowedTypes);
                throw new InvalidOperationException(
                    $"Configured {typeRequirement.DisplayName} column " +
                    $"'{typeRequirement.Entity}.{typeRequirement.Attribute}' is {metadata.AttributeType}; " +
                    $"expected {expected}.");
            }
        }
    }

    private static void ValidateConfiguredName(string settingName, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("REPLACE_WITH_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{settingName} is not configured. Replace its placeholder in appsettings.Local.json.");
        }
    }
}
