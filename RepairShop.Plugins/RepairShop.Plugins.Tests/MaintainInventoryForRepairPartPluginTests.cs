using System;
using System.Collections.Generic;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Plugins.RepairPart;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class MaintainInventoryForRepairPartPluginTests
    {
        [Fact]
        public void Create_SubtractsStock()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 3, stock: 10, unitCost: 25m);

            scenario.Execute();

            Assert.Equal(7, GetStockUpdate(scenario, scenario.NewInventoryItemId));
        }

        [Fact]
        public void Create_SnapshotsUnitCostAndTotalCost()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 3, stock: 10, unitCost: 12.50m);

            scenario.Execute();

            Entity target = Assert.IsType<Entity>(scenario.Target);
            Assert.Equal(12.50m, target.GetAttributeValue<Money>(RepairPartSchema.UnitCost).Value);
            Assert.Equal(37.50m, target.GetAttributeValue<Money>(RepairPartSchema.TotalCost).Value);
        }

        [Fact]
        public void Create_InsufficientStockRejectsRepairPart()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 3, stock: 2, unitCost: 25m);

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                scenario.Execute);

            Assert.Contains("enough stock", exception.Message);
            Assert.Empty(scenario.UpdateRequests);
        }

        [Fact]
        public void Create_InvalidQuantityRejectsRepairPart()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 0, stock: 10, unitCost: 25m);

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                scenario.Execute);

            Assert.Contains("positive Repair Part Quantity", exception.Message);
            Assert.Empty(scenario.UpdateRequests);
            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
        }

        [Fact]
        public void UpdateSameItem_QuantityIncreaseConsumesOnlyDifference()
        {
            Scenario scenario = Scenario.ForUpdateSameItem(
                oldQuantity: 2,
                newQuantity: 5,
                stock: 8,
                unitCost: 25m);

            scenario.Execute();

            Assert.Equal(5, GetStockUpdate(scenario, scenario.NewInventoryItemId));
        }

        [Fact]
        public void UpdateSameItem_QuantityDecreaseRestoresOnlyDifference()
        {
            Scenario scenario = Scenario.ForUpdateSameItem(
                oldQuantity: 5,
                newQuantity: 2,
                stock: 5,
                unitCost: 25m);

            scenario.Execute();

            Assert.Equal(8, GetStockUpdate(scenario, scenario.NewInventoryItemId));
        }

        [Fact]
        public void UpdateChangedItem_RestoresOldStockAndConsumesNewStock()
        {
            Scenario scenario = Scenario.ForUpdateChangedItem(
                oldQuantity: 2,
                newQuantity: 4,
                oldStock: 8,
                newStock: 10,
                newUnitCost: 30m);

            scenario.Execute();

            Assert.Equal(10, GetStockUpdate(scenario, scenario.OldInventoryItemId));
            Assert.Equal(6, GetStockUpdate(scenario, scenario.NewInventoryItemId));
        }

        [Fact]
        public void UpdateChangedItem_SnapshotsNewInventoryUnitCost()
        {
            Scenario scenario = Scenario.ForUpdateChangedItem(
                oldQuantity: 2,
                newQuantity: 4,
                oldStock: 8,
                newStock: 10,
                newUnitCost: 30m);

            scenario.Execute();

            Entity target = Assert.IsType<Entity>(scenario.Target);
            Assert.Equal(30m, target.GetAttributeValue<Money>(RepairPartSchema.UnitCost).Value);
            Assert.Equal(120m, target.GetAttributeValue<Money>(RepairPartSchema.TotalCost).Value);
        }

        [Fact]
        public void Delete_RestoresStock()
        {
            Scenario scenario = Scenario.ForDelete(quantity: 2, stock: 8);

            scenario.Execute();

            Assert.Equal(10, GetStockUpdate(scenario, scenario.OldInventoryItemId));
        }

        [Fact]
        public void Create_ExactStockConsumptionSucceeds()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 3, stock: 3, unitCost: 25m);

            scenario.Execute();

            Assert.Equal(0, GetStockUpdate(scenario, scenario.NewInventoryItemId));
        }

        [Fact]
        public void UnrelatedUpdate_PerformsNoInventoryWork()
        {
            Scenario scenario = Scenario.ForUnrelatedUpdate();

            scenario.Execute();

            Assert.Empty(scenario.UpdateRequests);
            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
        }

        [Fact]
        public void SameValueUpdate_PerformsNoInventoryWork()
        {
            Scenario scenario = Scenario.ForUpdateSameItem(
                oldQuantity: 2,
                newQuantity: 2,
                stock: 8,
                unitCost: 25m);

            scenario.Execute();

            Assert.Empty(scenario.UpdateRequests);
            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
        }

        [Fact]
        public void Update_MissingPreImageProducesConfigurationError()
        {
            Scenario scenario = Scenario.ForUpdateSameItem(
                oldQuantity: 2,
                newQuantity: 5,
                stock: 8,
                unitCost: 25m);
            scenario.PreImage = null;

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                scenario.Execute);

            Assert.Contains("required Pre Image", exception.Message);
            Assert.Contains(MaintainInventoryForRepairPartPlugin.PreImageAlias, exception.Message);
        }

        [Fact]
        public void InventoryUpdate_UsesSparseOptimisticConcurrencyRequest()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 3, stock: 10, unitCost: 25m);

            scenario.Execute();

            UpdateRequest request = Assert.Single(scenario.UpdateRequests);
            Assert.Equal(ConcurrencyBehavior.IfRowVersionMatches, request.ConcurrencyBehavior);
            Assert.Equal(Scenario.RowVersion, request.Target.RowVersion);
            Assert.Equal(InventorySchema.EntityLogicalName, request.Target.LogicalName);
            Assert.Single(request.Target.Attributes);
            Assert.True(request.Target.Contains(InventorySchema.StockQuantity));
            Assert.False(request.Target.Contains("versionnumber"));
        }

        [Fact]
        public void Create_CompletedRepairIsBlocked()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 1, stock: 10, unitCost: 25m);
            scenario.SetParentCase(RepairStatusValues.Completed);

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                scenario.Execute);

            Assert.Contains("Completed", exception.Message);
            Assert.Empty(scenario.UpdateRequests);
        }

        [Fact]
        public void Update_CancelledRepairIsBlocked()
        {
            Scenario scenario = Scenario.ForUpdateSameItem(
                oldQuantity: 2,
                newQuantity: 3,
                stock: 8,
                unitCost: 25m);
            scenario.SetParentCase(RepairStatusValues.Cancelled);

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                scenario.Execute);

            Assert.Contains("Cancelled", exception.Message);
            Assert.Empty(scenario.UpdateRequests);
        }

        [Fact]
        public void Create_LowStockThresholdIsTracedWithoutAdditionalAction()
        {
            Scenario scenario = Scenario.ForCreate(
                quantity: 3,
                stock: 5,
                unitCost: 25m,
                reorderLevel: 2);

            scenario.Execute();

            Assert.Single(scenario.UpdateRequests);
            Assert.Contains(
                scenario.TracingService.Messages,
                message => message.Contains("Low stock") &&
                    message.Contains("No reorder action was created"));
        }

        [Fact]
        public void InventoryUpdateFailure_RejectsRepairPartOperation()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 1, stock: 10, unitCost: 25m);
            scenario.ExecuteFailure = new Exception("Simulated inventory write failure.");

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                scenario.Execute);

            Assert.Contains("Repair Part inventory maintenance failed", exception.Message);
            Entity target = Assert.IsType<Entity>(scenario.Target);
            Assert.False(target.Contains(RepairPartSchema.UnitCost));
            Assert.False(target.Contains(RepairPartSchema.TotalCost));
        }

        [Fact]
        public void ConcurrencyConflict_RejectsOperationWithRetryMessage()
        {
            Scenario scenario = Scenario.ForCreate(quantity: 1, stock: 10, unitCost: 25m);
            var fault = new OrganizationServiceFault
            {
                ErrorCode = -2147088254,
                Message = "Concurrency version mismatch."
            };
            scenario.ExecuteFailure = new FaultException<OrganizationServiceFault>(
                fault,
                new FaultReason(fault.Message));

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                scenario.Execute);

            Assert.Contains("changed during this operation", exception.Message);
            Assert.Contains("try again", exception.Message);
        }

        private static int GetStockUpdate(Scenario scenario, Guid inventoryItemId)
        {
            UpdateRequest request = Assert.Single(
                scenario.UpdateRequests,
                item => item.Target.Id == inventoryItemId);
            return request.Target.GetAttributeValue<int>(InventorySchema.StockQuantity);
        }

        private sealed class Scenario
        {
            internal const string RowVersion = "123456";

            private readonly IDictionary<Guid, Entity> inventoryById =
                new Dictionary<Guid, Entity>();
            private readonly IDictionary<Guid, Entity> casesById =
                new Dictionary<Guid, Entity>();
            private readonly string messageName;

            private Scenario(string messageName, object target, Entity preImage)
            {
                this.messageName = messageName;
                Target = target;
                PreImage = preImage;
                OrganizationService = new Mock<IOrganizationService>();
                TracingService = new TestTracingService();
                UpdateRequests = new List<UpdateRequest>();

                OrganizationService
                    .Setup(service => service.Retrieve(
                        InventorySchema.EntityLogicalName,
                        It.IsAny<Guid>(),
                        It.IsAny<ColumnSet>()))
                    .Returns((string _, Guid id, ColumnSet __) => inventoryById[id]);
                OrganizationService
                    .Setup(service => service.Retrieve(
                        IncidentSchema.EntityLogicalName,
                        It.IsAny<Guid>(),
                        It.IsAny<ColumnSet>()))
                    .Returns((string _, Guid id, ColumnSet __) => casesById[id]);
                OrganizationService
                    .Setup(service => service.Execute(It.IsAny<OrganizationRequest>()))
                    .Returns((OrganizationRequest request) =>
                    {
                        UpdateRequests.Add(Assert.IsType<UpdateRequest>(request));
                        if (ExecuteFailure != null)
                        {
                            throw ExecuteFailure;
                        }

                        return new UpdateResponse();
                    });
            }

            internal object Target { get; }

            internal Entity PreImage { get; set; }

            internal Guid OldInventoryItemId { get; private set; }

            internal Guid NewInventoryItemId { get; private set; }

            internal Mock<IOrganizationService> OrganizationService { get; }

            internal TestTracingService TracingService { get; }

            internal IList<UpdateRequest> UpdateRequests { get; }

            internal Exception ExecuteFailure { get; set; }

            internal static Scenario ForCreate(
                int quantity,
                int stock,
                decimal unitCost,
                int? reorderLevel = 3)
            {
                Guid inventoryItemId = Guid.NewGuid();
                var target = new Entity(RepairPartSchema.EntityLogicalName)
                {
                    [RepairPartSchema.InventoryItem] = new EntityReference(
                        InventorySchema.EntityLogicalName,
                        inventoryItemId),
                    [RepairPartSchema.Quantity] = quantity
                };
                var scenario = new Scenario("Create", target, preImage: null)
                {
                    NewInventoryItemId = inventoryItemId
                };
                scenario.AddInventory(inventoryItemId, stock, unitCost, reorderLevel);
                return scenario;
            }

            internal static Scenario ForUpdateSameItem(
                int oldQuantity,
                int newQuantity,
                int stock,
                decimal unitCost)
            {
                Guid inventoryItemId = Guid.NewGuid();
                var target = new Entity(RepairPartSchema.EntityLogicalName, Guid.NewGuid())
                {
                    [RepairPartSchema.Quantity] = newQuantity
                };
                Entity preImage = CreatePreImage(inventoryItemId, oldQuantity);
                var scenario = new Scenario("Update", target, preImage)
                {
                    OldInventoryItemId = inventoryItemId,
                    NewInventoryItemId = inventoryItemId
                };
                scenario.AddInventory(inventoryItemId, stock, unitCost, reorderLevel: 3);
                return scenario;
            }

            internal static Scenario ForUpdateChangedItem(
                int oldQuantity,
                int newQuantity,
                int oldStock,
                int newStock,
                decimal newUnitCost)
            {
                Guid oldInventoryItemId = Guid.NewGuid();
                Guid newInventoryItemId = Guid.NewGuid();
                var target = new Entity(RepairPartSchema.EntityLogicalName, Guid.NewGuid())
                {
                    [RepairPartSchema.InventoryItem] = new EntityReference(
                        InventorySchema.EntityLogicalName,
                        newInventoryItemId),
                    [RepairPartSchema.Quantity] = newQuantity
                };
                Entity preImage = CreatePreImage(oldInventoryItemId, oldQuantity);
                var scenario = new Scenario("Update", target, preImage)
                {
                    OldInventoryItemId = oldInventoryItemId,
                    NewInventoryItemId = newInventoryItemId
                };
                scenario.AddInventory(oldInventoryItemId, oldStock, 20m, reorderLevel: 3);
                scenario.AddInventory(newInventoryItemId, newStock, newUnitCost, reorderLevel: 3);
                return scenario;
            }

            internal static Scenario ForDelete(int quantity, int stock)
            {
                Guid repairPartId = Guid.NewGuid();
                Guid inventoryItemId = Guid.NewGuid();
                Entity preImage = CreatePreImage(inventoryItemId, quantity);
                var scenario = new Scenario(
                    "Delete",
                    new EntityReference(RepairPartSchema.EntityLogicalName, repairPartId),
                    preImage)
                {
                    OldInventoryItemId = inventoryItemId
                };
                scenario.AddInventory(inventoryItemId, stock, 25m, reorderLevel: 3);
                return scenario;
            }

            internal static Scenario ForUnrelatedUpdate()
            {
                var target = new Entity(RepairPartSchema.EntityLogicalName, Guid.NewGuid())
                {
                    ["gsic_repairpartname"] = "Changed name"
                };
                return new Scenario("Update", target, preImage: null);
            }

            internal void SetParentCase(int repairStatus)
            {
                Guid caseId = Guid.NewGuid();
                var repairCase = new Entity(IncidentSchema.EntityLogicalName, caseId)
                {
                    [IncidentSchema.RepairStatus] = new OptionSetValue(repairStatus)
                };
                casesById.Add(caseId, repairCase);

                Entity targetEntity = Target as Entity;
                if (targetEntity != null && string.Equals(messageName, "Create", StringComparison.Ordinal))
                {
                    targetEntity[RepairPartSchema.Case] = new EntityReference(
                        IncidentSchema.EntityLogicalName,
                        caseId);
                }
                else
                {
                    PreImage[RepairPartSchema.Case] = new EntityReference(
                        IncidentSchema.EntityLogicalName,
                        caseId);
                }
            }

            internal void Execute()
            {
                Guid primaryEntityId = Target is Entity entity
                    ? entity.Id
                    : ((EntityReference)Target).Id;
                Mock<IPluginExecutionContext> context = PluginTestContext.Create(
                    RepairPartSchema.EntityLogicalName,
                    stage: 20,
                    primaryEntityId,
                    Target,
                    PreImage,
                    messageName: messageName);
                var factory = new Mock<IOrganizationServiceFactory>();
                factory
                    .Setup(item => item.CreateOrganizationService(context.Object.UserId))
                    .Returns(OrganizationService.Object);
                var provider = new TestServiceProvider()
                    .Add<ITracingService>(TracingService)
                    .Add<IPluginExecutionContext>(context.Object)
                    .Add<IOrganizationServiceFactory>(factory.Object);

                new MaintainInventoryForRepairPartPlugin().Execute(provider);
            }

            private static Entity CreatePreImage(Guid inventoryItemId, int quantity)
            {
                return new Entity(RepairPartSchema.EntityLogicalName, Guid.NewGuid())
                {
                    [RepairPartSchema.InventoryItem] = new EntityReference(
                        InventorySchema.EntityLogicalName,
                        inventoryItemId),
                    [RepairPartSchema.Quantity] = quantity
                };
            }

            private void AddInventory(
                Guid id,
                int stock,
                decimal unitCost,
                int? reorderLevel)
            {
                var inventory = new Entity(InventorySchema.EntityLogicalName, id)
                {
                    RowVersion = RowVersion,
                    [InventorySchema.StockQuantity] = stock,
                    [InventorySchema.UnitCost] = new Money(unitCost)
                };
                if (reorderLevel.HasValue)
                {
                    inventory[InventorySchema.ReorderLevel] = reorderLevel.Value;
                }

                inventoryById.Add(id, inventory);
            }
        }
    }
}
