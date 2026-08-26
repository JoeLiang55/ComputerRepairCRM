using System;
using System.Collections.Generic;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Services;

namespace RepairShop.Plugins.Plugins.RepairPart
{
    /// <summary>
    /// Maintains Inventory stock and Repair Part cost snapshots in the initiating transaction.
    /// Register synchronous PreOperation steps for Create, Update, and Delete of Repair Part.
    /// </summary>
    public sealed class MaintainInventoryForRepairPartPlugin : PluginBase
    {
        public const string PreImageAlias = "PreImage";

        private const int PreOperationStage = 20;
        private const string CreateMessage = "Create";
        private const string UpdateMessage = "Update";
        private const string DeleteMessage = "Delete";

        // Dataverse error code ConcurrencyVersionMismatch.
        private const int ConcurrencyVersionMismatchErrorCode = -2147088254;

        private readonly InventoryAllocationService allocationService;

        public MaintainInventoryForRepairPartPlugin()
            : this(new InventoryAllocationService())
        {
        }

        internal MaintainInventoryForRepairPartPlugin(
            InventoryAllocationService allocationService)
        {
            this.allocationService = allocationService ??
                throw new ArgumentNullException(nameof(allocationService));
        }

        protected override string UnexpectedErrorMessage =>
            "Repair Part inventory maintenance failed.";

        protected override void ExecutePlugin(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            bool isCreate = IsMessage(context, CreateMessage);
            bool isUpdate = IsMessage(context, UpdateMessage);
            bool isDelete = IsMessage(context, DeleteMessage);
            if ((!isCreate && !isUpdate && !isDelete) ||
                !string.Equals(
                    context.PrimaryEntityName,
                    RepairPartSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase) ||
                context.Stage != PreOperationStage)
            {
                tracingService.Trace(
                    "Execution skipped: expected Create, Update, or Delete on {0} at PreOperation; received {1}/{2}/stage {3}.",
                    RepairPartSchema.EntityLogicalName,
                    context.MessageName,
                    context.PrimaryEntityName,
                    context.Stage);
                return;
            }

            if (isDelete)
            {
                ExecuteDelete(serviceProvider, context, tracingService);
                return;
            }

            Entity target = GetRequiredEntityTarget(context);
            if (isUpdate &&
                !target.Contains(RepairPartSchema.InventoryItem) &&
                !target.Contains(RepairPartSchema.Quantity))
            {
                tracingService.Trace(
                    "Execution skipped: Update did not submit Inventory Item or Quantity.");
                return;
            }

            if (isCreate)
            {
                ExecuteCreate(serviceProvider, context, target, tracingService);
                return;
            }

            ExecuteUpdate(serviceProvider, context, target, tracingService);
        }

        private void ExecuteCreate(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            Entity target,
            ITracingService tracingService)
        {
            EntityReference inventoryItem = GetRequiredReference(
                target,
                RepairPartSchema.InventoryItem,
                InventorySchema.EntityLogicalName,
                "Select an Inventory Item before saving the Repair Part.");
            int quantity = GetRequiredQuantity(
                target,
                RepairPartSchema.Quantity,
                "Enter a positive Repair Part Quantity before saving.");

            IOrganizationService organizationService = GetOrganizationService(
                serviceProvider,
                context.UserId);
            ValidateAvailableParentCases(
                organizationService,
                target,
                preImage: null,
                tracingService);

            InventoryRecord inventory = RetrieveInventory(
                organizationService,
                inventoryItem.Id);
            InventoryAllocationResult result = CalculateAllocation(
                () => allocationService.CalculateCreate(
                    inventoryItem.Id,
                    quantity,
                    inventory.StockQuantity,
                    inventory.UnitCost));

            ApplyInventoryAdjustments(
                organizationService,
                result.StockAdjustments,
                new Dictionary<Guid, InventoryRecord>
                {
                    { inventory.Id, inventory }
                },
                tracingService);
            ApplyCostSnapshot(target, result.CostSnapshot);
        }

        private void ExecuteUpdate(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            Entity target,
            ITracingService tracingService)
        {
            Entity preImage = GetRequiredPreImage(context);
            EntityReference oldInventoryItem = GetRequiredImageReference(
                preImage,
                RepairPartSchema.InventoryItem,
                InventorySchema.EntityLogicalName);
            int oldQuantity = GetRequiredImageQuantity(
                preImage,
                RepairPartSchema.Quantity);
            EntityReference newInventoryItem = target.Contains(RepairPartSchema.InventoryItem)
                ? GetRequiredReference(
                    target,
                    RepairPartSchema.InventoryItem,
                    InventorySchema.EntityLogicalName,
                    "Select an Inventory Item before saving the Repair Part.")
                : oldInventoryItem;
            int newQuantity = target.Contains(RepairPartSchema.Quantity)
                ? GetRequiredQuantity(
                    target,
                    RepairPartSchema.Quantity,
                    "Enter a positive Repair Part Quantity before saving.")
                : oldQuantity;

            if (oldInventoryItem.Id == newInventoryItem.Id && oldQuantity == newQuantity)
            {
                tracingService.Trace(
                    "Execution skipped: effective Inventory Item and Quantity did not change.");
                return;
            }

            IOrganizationService organizationService = GetOrganizationService(
                serviceProvider,
                context.UserId);
            ValidateAvailableParentCases(
                organizationService,
                target,
                preImage,
                tracingService);

            var inventoryById = new Dictionary<Guid, InventoryRecord>();
            InventoryRecord oldInventory = RetrieveInventory(
                organizationService,
                oldInventoryItem.Id);
            inventoryById.Add(oldInventory.Id, oldInventory);

            InventoryRecord newInventory;
            if (oldInventoryItem.Id == newInventoryItem.Id)
            {
                newInventory = oldInventory;
            }
            else
            {
                newInventory = RetrieveInventory(
                    organizationService,
                    newInventoryItem.Id);
                inventoryById.Add(newInventory.Id, newInventory);
            }

            var currentStockByInventoryItem = new Dictionary<Guid, int?>();
            foreach (KeyValuePair<Guid, InventoryRecord> pair in inventoryById)
            {
                currentStockByInventoryItem.Add(pair.Key, pair.Value.StockQuantity);
            }

            InventoryAllocationResult result = CalculateAllocation(
                () => allocationService.CalculateUpdate(
                    oldInventoryItem.Id,
                    oldQuantity,
                    newInventoryItem.Id,
                    newQuantity,
                    currentStockByInventoryItem,
                    newInventory.UnitCost));

            ApplyInventoryAdjustments(
                organizationService,
                result.StockAdjustments,
                inventoryById,
                tracingService);
            ApplyCostSnapshot(target, result.CostSnapshot);
        }

        private void ExecuteDelete(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            ValidateDeleteTarget(context);
            Entity preImage = GetRequiredPreImage(context);
            EntityReference inventoryItem = GetRequiredImageReference(
                preImage,
                RepairPartSchema.InventoryItem,
                InventorySchema.EntityLogicalName);
            int quantity = GetRequiredImageQuantity(
                preImage,
                RepairPartSchema.Quantity);

            IOrganizationService organizationService = GetOrganizationService(
                serviceProvider,
                context.UserId);
            ValidateAvailableParentCases(
                organizationService,
                target: null,
                preImage,
                tracingService);

            InventoryRecord inventory = RetrieveInventory(
                organizationService,
                inventoryItem.Id);
            InventoryAllocationResult result = CalculateAllocation(
                () => allocationService.CalculateDelete(
                    inventoryItem.Id,
                    quantity,
                    inventory.StockQuantity));

            ApplyInventoryAdjustments(
                organizationService,
                result.StockAdjustments,
                new Dictionary<Guid, InventoryRecord>
                {
                    { inventory.Id, inventory }
                },
                tracingService);
        }

        private static InventoryAllocationResult CalculateAllocation(
            Func<InventoryAllocationResult> calculate)
        {
            try
            {
                return calculate();
            }
            catch (ArgumentException exception)
            {
                throw new InvalidPluginExecutionException(exception.Message, exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidPluginExecutionException(exception.Message, exception);
            }
        }

        private static void ApplyInventoryAdjustments(
            IOrganizationService organizationService,
            IReadOnlyList<InventoryStockAdjustment> adjustments,
            IReadOnlyDictionary<Guid, InventoryRecord> inventoryById,
            ITracingService tracingService)
        {
            foreach (InventoryStockAdjustment adjustment in adjustments)
            {
                if (adjustment.StockDelta == 0)
                {
                    tracingService.Trace(
                        "Inventory {0} update skipped because its stock delta is zero.",
                        adjustment.InventoryItemId);
                    continue;
                }

                InventoryRecord inventory;
                if (!inventoryById.TryGetValue(adjustment.InventoryItemId, out inventory))
                {
                    throw new InvalidPluginExecutionException(
                        "An affected Inventory record was not retrieved.");
                }

                UpdateInventoryWithConcurrency(
                    organizationService,
                    inventory,
                    adjustment.ResultingStock);
                tracingService.Trace(
                    "Inventory {0} stock changed by {1}. Previous={2}; Resulting={3}.",
                    adjustment.InventoryItemId,
                    adjustment.StockDelta,
                    adjustment.CurrentStock,
                    adjustment.ResultingStock);

                if (inventory.ReorderLevel.HasValue &&
                    adjustment.ResultingStock <= inventory.ReorderLevel.Value)
                {
                    tracingService.Trace(
                        "Low stock: Inventory {0} resulting stock {1} is at or below Reorder Level {2}. No reorder action was created.",
                        adjustment.InventoryItemId,
                        adjustment.ResultingStock,
                        inventory.ReorderLevel.Value);
                }
            }
        }

        private static void UpdateInventoryWithConcurrency(
            IOrganizationService organizationService,
            InventoryRecord inventory,
            int resultingStock)
        {
            if (string.IsNullOrWhiteSpace(inventory.RowVersion))
            {
                throw new InvalidPluginExecutionException(
                    "Inventory stock could not be updated because its row version was unavailable.");
            }

            var update = new Entity(InventorySchema.EntityLogicalName, inventory.Id)
            {
                RowVersion = inventory.RowVersion
            };
            update[InventorySchema.StockQuantity] = resultingStock;
            var request = new UpdateRequest
            {
                Target = update,
                ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches
            };

            try
            {
                organizationService.Execute(request);
            }
            catch (FaultException<OrganizationServiceFault> exception)
                when (ContainsErrorCode(
                    exception.Detail,
                    ConcurrencyVersionMismatchErrorCode))
            {
                throw new InvalidPluginExecutionException(
                    "Inventory stock changed during this operation. Refresh the Repair Part and try again.",
                    exception);
            }
        }

        private static bool ContainsErrorCode(
            OrganizationServiceFault fault,
            int errorCode)
        {
            while (fault != null)
            {
                if (fault.ErrorCode == errorCode)
                {
                    return true;
                }

                fault = fault.InnerFault;
            }

            return false;
        }

        private static InventoryRecord RetrieveInventory(
            IOrganizationService organizationService,
            Guid inventoryItemId)
        {
            Entity entity = organizationService.Retrieve(
                InventorySchema.EntityLogicalName,
                inventoryItemId,
                new ColumnSet(
                    InventorySchema.StockQuantity,
                    InventorySchema.UnitCost,
                    InventorySchema.ReorderLevel));
            if (entity == null)
            {
                throw new InvalidPluginExecutionException(
                    "The selected Inventory item could not be retrieved.");
            }

            int? stockQuantity = GetNullableInt(entity, InventorySchema.StockQuantity);
            int? reorderLevel = GetNullableInt(entity, InventorySchema.ReorderLevel);
            Money unitCost = entity.GetAttributeValue<Money>(InventorySchema.UnitCost);
            return new InventoryRecord(
                entity.Id,
                entity.RowVersion,
                stockQuantity,
                unitCost == null ? (decimal?)null : unitCost.Value,
                reorderLevel);
        }

        private static int? GetNullableInt(Entity entity, string attributeName)
        {
            return entity.Contains(attributeName) && entity[attributeName] != null
                ? entity.GetAttributeValue<int>(attributeName)
                : (int?)null;
        }

        private static void ApplyCostSnapshot(
            Entity target,
            InventoryCostSnapshot costSnapshot)
        {
            if (costSnapshot == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Repair Part cost snapshot could not be calculated.");
            }

            target[RepairPartSchema.UnitCost] = new Money(costSnapshot.UnitCost);
            target[RepairPartSchema.TotalCost] = new Money(costSnapshot.TotalCost);
        }

        private static void ValidateAvailableParentCases(
            IOrganizationService organizationService,
            Entity target,
            Entity preImage,
            ITracingService tracingService)
        {
            var caseIds = new HashSet<Guid>();
            AddCaseIfAvailable(caseIds, target);
            AddCaseIfAvailable(caseIds, preImage);

            foreach (Guid caseId in caseIds)
            {
                Entity repairCase = organizationService.Retrieve(
                    IncidentSchema.EntityLogicalName,
                    caseId,
                    new ColumnSet(IncidentSchema.RepairStatus));
                OptionSetValue repairStatus = repairCase == null
                    ? null
                    : repairCase.GetAttributeValue<OptionSetValue>(
                        IncidentSchema.RepairStatus);
                if (repairStatus == null)
                {
                    tracingService.Trace(
                        "Terminal repair validation skipped for Case {0}: Repair Status is unavailable.",
                        caseId);
                    continue;
                }

                if (repairStatus.Value == RepairStatusValues.Completed ||
                    repairStatus.Value == RepairStatusValues.Cancelled)
                {
                    throw new InvalidPluginExecutionException(
                        string.Format(
                            "Repair Parts cannot be changed because the parent Case is {0}.",
                            RepairStatusTransitionValidator.GetStatusName(
                                repairStatus.Value)));
                }
            }
        }

        private static void AddCaseIfAvailable(ISet<Guid> caseIds, Entity entity)
        {
            if (entity == null || !entity.Contains(RepairPartSchema.Case))
            {
                return;
            }

            EntityReference repairCase = entity.GetAttributeValue<EntityReference>(
                RepairPartSchema.Case);
            if (repairCase == null)
            {
                return;
            }

            if (repairCase.Id == Guid.Empty ||
                !string.Equals(
                    repairCase.LogicalName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException(
                    "The Repair Part contains an invalid Case reference.");
            }

            caseIds.Add(repairCase.Id);
        }

        private static Entity GetRequiredEntityTarget(IPluginExecutionContext context)
        {
            Entity target = context.InputParameters != null &&
                context.InputParameters.Contains("Target")
                ? context.InputParameters["Target"] as Entity
                : null;
            if (target == null ||
                !string.Equals(
                    target.LogicalName,
                    RepairPartSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException(
                    "The Repair Part operation does not contain a valid Entity Target.");
            }

            return target;
        }

        private static void ValidateDeleteTarget(IPluginExecutionContext context)
        {
            EntityReference target = context.InputParameters != null &&
                context.InputParameters.Contains("Target")
                ? context.InputParameters["Target"] as EntityReference
                : null;
            if (target == null ||
                target.Id == Guid.Empty ||
                !string.Equals(
                    target.LogicalName,
                    RepairPartSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException(
                    "The Repair Part Delete operation does not contain a valid EntityReference Target.");
            }
        }

        private static Entity GetRequiredPreImage(IPluginExecutionContext context)
        {
            Entity preImage;
            if (context.PreEntityImages == null ||
                !context.PreEntityImages.TryGetValue(PreImageAlias, out preImage) ||
                preImage == null)
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The plug-in step is missing the required Pre Image with alias '{0}'.",
                        PreImageAlias));
            }

            return preImage;
        }

        private static EntityReference GetRequiredImageReference(
            Entity preImage,
            string attributeName,
            string expectedLogicalName)
        {
            if (!preImage.Contains(attributeName))
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The plug-in Pre Image must contain the {0} attribute.",
                        attributeName));
            }

            return GetRequiredReference(
                preImage,
                attributeName,
                expectedLogicalName,
                string.Format(
                    "The plug-in Pre Image does not contain a valid {0} value.",
                    attributeName));
        }

        private static int GetRequiredImageQuantity(
            Entity preImage,
            string attributeName)
        {
            if (!preImage.Contains(attributeName))
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The plug-in Pre Image must contain the {0} attribute.",
                        attributeName));
            }

            return GetRequiredQuantity(
                preImage,
                attributeName,
                string.Format(
                    "The plug-in Pre Image does not contain a positive {0} value.",
                    attributeName));
        }

        private static EntityReference GetRequiredReference(
            Entity entity,
            string attributeName,
            string expectedLogicalName,
            string errorMessage)
        {
            EntityReference reference = entity.GetAttributeValue<EntityReference>(attributeName);
            if (reference == null ||
                reference.Id == Guid.Empty ||
                !string.Equals(
                    reference.LogicalName,
                    expectedLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException(errorMessage);
            }

            return reference;
        }

        private static int GetRequiredQuantity(
            Entity entity,
            string attributeName,
            string errorMessage)
        {
            if (!entity.Contains(attributeName) || entity[attributeName] == null)
            {
                throw new InvalidPluginExecutionException(errorMessage);
            }

            int quantity = entity.GetAttributeValue<int>(attributeName);
            if (quantity <= 0)
            {
                throw new InvalidPluginExecutionException(errorMessage);
            }

            return quantity;
        }

        private static IOrganizationService GetOrganizationService(
            IServiceProvider serviceProvider,
            Guid userId)
        {
            var factory =
                (IOrganizationServiceFactory)serviceProvider.GetService(
                    typeof(IOrganizationServiceFactory));
            if (factory == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Dataverse organization service factory is unavailable.");
            }

            IOrganizationService service = factory.CreateOrganizationService(userId);
            if (service == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Dataverse organization service is unavailable.");
            }

            return service;
        }

        private static bool IsMessage(
            IPluginExecutionContext context,
            string messageName)
        {
            return string.Equals(
                context.MessageName,
                messageName,
                StringComparison.OrdinalIgnoreCase);
        }

        private sealed class InventoryRecord
        {
            internal InventoryRecord(
                Guid id,
                string rowVersion,
                int? stockQuantity,
                decimal? unitCost,
                int? reorderLevel)
            {
                Id = id;
                RowVersion = rowVersion;
                StockQuantity = stockQuantity;
                UnitCost = unitCost;
                ReorderLevel = reorderLevel;
            }

            internal Guid Id { get; }

            internal string RowVersion { get; }

            internal int? StockQuantity { get; }

            internal decimal? UnitCost { get; }

            internal int? ReorderLevel { get; }
        }
    }
}
