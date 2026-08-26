using System;
using System.Collections.Generic;
using RepairShop.Plugins.Services;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class InventoryAllocationServiceTests
    {
        private readonly InventoryAllocationService service =
            new InventoryAllocationService();

        [Fact]
        public void Create_SubtractsNewQuantityFromSelectedInventoryItem()
        {
            Guid inventoryItemId = Guid.NewGuid();

            InventoryAllocationResult result = service.CalculateCreate(
                inventoryItemId,
                quantity: 3,
                currentStock: 10,
                inventoryUnitCost: 25m);

            InventoryStockAdjustment adjustment = Assert.Single(result.StockAdjustments);
            AssertAdjustment(adjustment, inventoryItemId, 10, -3, 7);
        }

        [Fact]
        public void UpdateSameItem_QuantityIncreaseConsumesOnlyDifference()
        {
            Guid inventoryItemId = Guid.NewGuid();

            InventoryAllocationResult result = service.CalculateUpdate(
                inventoryItemId,
                oldQuantity: 2,
                inventoryItemId,
                newQuantity: 5,
                Stocks((inventoryItemId, 8)),
                newInventoryUnitCost: 25m);

            InventoryStockAdjustment adjustment = Assert.Single(result.StockAdjustments);
            AssertAdjustment(adjustment, inventoryItemId, 8, -3, 5);
        }

        [Fact]
        public void UpdateSameItem_QuantityDecreaseRestoresOnlyDifference()
        {
            Guid inventoryItemId = Guid.NewGuid();

            InventoryAllocationResult result = service.CalculateUpdate(
                inventoryItemId,
                oldQuantity: 5,
                inventoryItemId,
                newQuantity: 2,
                Stocks((inventoryItemId, 5)),
                newInventoryUnitCost: 25m);

            InventoryStockAdjustment adjustment = Assert.Single(result.StockAdjustments);
            AssertAdjustment(adjustment, inventoryItemId, 5, 3, 8);
        }

        [Fact]
        public void UpdateChangedItem_RestoresOldAndConsumesNewQuantity()
        {
            Guid oldInventoryItemId = Guid.NewGuid();
            Guid newInventoryItemId = Guid.NewGuid();

            InventoryAllocationResult result = service.CalculateUpdate(
                oldInventoryItemId,
                oldQuantity: 2,
                newInventoryItemId,
                newQuantity: 4,
                Stocks((oldInventoryItemId, 8), (newInventoryItemId, 10)),
                newInventoryUnitCost: 30m);

            Assert.Collection(
                result.StockAdjustments,
                adjustment => AssertAdjustment(adjustment, oldInventoryItemId, 8, 2, 10),
                adjustment => AssertAdjustment(adjustment, newInventoryItemId, 10, -4, 6));
        }

        [Fact]
        public void Delete_RestoresOldQuantity()
        {
            Guid inventoryItemId = Guid.NewGuid();

            InventoryAllocationResult result = service.CalculateDelete(
                inventoryItemId,
                quantity: 2,
                currentStock: 8);

            InventoryStockAdjustment adjustment = Assert.Single(result.StockAdjustments);
            AssertAdjustment(adjustment, inventoryItemId, 8, 2, 10);
            Assert.Null(result.CostSnapshot);
        }

        [Fact]
        public void Create_ExactAvailableStockIsAllowed()
        {
            Guid inventoryItemId = Guid.NewGuid();

            InventoryAllocationResult result = service.CalculateCreate(
                inventoryItemId,
                quantity: 3,
                currentStock: 3,
                inventoryUnitCost: 25m);

            Assert.Equal(0, Assert.Single(result.StockAdjustments).ResultingStock);
        }

        [Fact]
        public void Create_InsufficientStockIsRejected()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => service.CalculateCreate(
                    Guid.NewGuid(),
                    quantity: 3,
                    currentStock: 2,
                    inventoryUnitCost: 25m));

            Assert.Contains("enough stock", exception.Message);
        }

        [Fact]
        public void Create_ZeroQuantityIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.CalculateCreate(
                    Guid.NewGuid(),
                    quantity: 0,
                    currentStock: 10,
                    inventoryUnitCost: 25m));
        }

        [Fact]
        public void Create_NegativeQuantityIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.CalculateCreate(
                    Guid.NewGuid(),
                    quantity: -1,
                    currentStock: 10,
                    inventoryUnitCost: 25m));
        }

        [Fact]
        public void Create_SnapshotsUnitCostAndCalculatesTotalCost()
        {
            InventoryAllocationResult result = service.CalculateCreate(
                Guid.NewGuid(),
                quantity: 3,
                currentStock: 10,
                inventoryUnitCost: 12.50m);

            Assert.NotNull(result.CostSnapshot);
            Assert.Equal(12.50m, result.CostSnapshot.UnitCost);
            Assert.Equal(37.50m, result.CostSnapshot.TotalCost);
        }

        [Fact]
        public void Create_NullStoredStockIsRejected()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => service.CalculateCreate(
                    Guid.NewGuid(),
                    quantity: 1,
                    currentStock: null,
                    inventoryUnitCost: 25m));

            Assert.Contains("valid Stock Quantity", exception.Message);
        }

        [Fact]
        public void Create_NegativeStoredStockIsRejected()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => service.CalculateCreate(
                    Guid.NewGuid(),
                    quantity: 1,
                    currentStock: -1,
                    inventoryUnitCost: 25m));

            Assert.Contains("cannot be negative", exception.Message);
        }

        private static IReadOnlyDictionary<Guid, int?> Stocks(
            params (Guid InventoryItemId, int? CurrentStock)[] values)
        {
            var result = new Dictionary<Guid, int?>();
            foreach ((Guid inventoryItemId, int? currentStock) in values)
            {
                result.Add(inventoryItemId, currentStock);
            }

            return result;
        }

        private static void AssertAdjustment(
            InventoryStockAdjustment adjustment,
            Guid inventoryItemId,
            int currentStock,
            int stockDelta,
            int resultingStock)
        {
            Assert.Equal(inventoryItemId, adjustment.InventoryItemId);
            Assert.Equal(currentStock, adjustment.CurrentStock);
            Assert.Equal(stockDelta, adjustment.StockDelta);
            Assert.Equal(resultingStock, adjustment.ResultingStock);
        }
    }
}
