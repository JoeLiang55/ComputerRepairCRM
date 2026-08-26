using System;
using System.Collections.Generic;

namespace RepairShop.Plugins.Services
{
    /// <summary>
    /// Calculates Repair Part inventory changes without performing Dataverse operations.
    /// A negative stock delta consumes inventory; a positive delta restores inventory.
    /// </summary>
    internal sealed class InventoryAllocationService
    {
        internal InventoryAllocationResult CalculateCreate(
            Guid inventoryItemId,
            int quantity,
            int? currentStock,
            decimal? inventoryUnitCost)
        {
            ValidateInventoryItemId(inventoryItemId, nameof(inventoryItemId));
            ValidateQuantity(quantity, nameof(quantity));

            InventoryStockAdjustment adjustment = CalculateStockAdjustment(
                inventoryItemId,
                currentStock,
                -quantity);
            InventoryCostSnapshot costSnapshot = CalculateCostSnapshot(
                inventoryUnitCost,
                quantity);

            return new InventoryAllocationResult(
                new[] { adjustment },
                costSnapshot);
        }

        internal InventoryAllocationResult CalculateUpdate(
            Guid oldInventoryItemId,
            int oldQuantity,
            Guid newInventoryItemId,
            int newQuantity,
            IReadOnlyDictionary<Guid, int?> currentStockByInventoryItem,
            decimal? newInventoryUnitCost)
        {
            ValidateInventoryItemId(oldInventoryItemId, nameof(oldInventoryItemId));
            ValidateInventoryItemId(newInventoryItemId, nameof(newInventoryItemId));
            ValidateQuantity(oldQuantity, nameof(oldQuantity));
            ValidateQuantity(newQuantity, nameof(newQuantity));

            if (currentStockByInventoryItem == null)
            {
                throw new ArgumentNullException(nameof(currentStockByInventoryItem));
            }

            InventoryCostSnapshot costSnapshot = CalculateCostSnapshot(
                newInventoryUnitCost,
                newQuantity);

            if (oldInventoryItemId == newInventoryItemId)
            {
                int stockDelta = checked(oldQuantity - newQuantity);
                InventoryStockAdjustment adjustment = CalculateStockAdjustment(
                    newInventoryItemId,
                    GetCurrentStock(currentStockByInventoryItem, newInventoryItemId),
                    stockDelta);

                return new InventoryAllocationResult(
                    new[] { adjustment },
                    costSnapshot);
            }

            InventoryStockAdjustment oldItemAdjustment = CalculateStockAdjustment(
                oldInventoryItemId,
                GetCurrentStock(currentStockByInventoryItem, oldInventoryItemId),
                oldQuantity);
            InventoryStockAdjustment newItemAdjustment = CalculateStockAdjustment(
                newInventoryItemId,
                GetCurrentStock(currentStockByInventoryItem, newInventoryItemId),
                -newQuantity);

            return new InventoryAllocationResult(
                new[] { oldItemAdjustment, newItemAdjustment },
                costSnapshot);
        }

        internal InventoryAllocationResult CalculateDelete(
            Guid inventoryItemId,
            int quantity,
            int? currentStock)
        {
            ValidateInventoryItemId(inventoryItemId, nameof(inventoryItemId));
            ValidateQuantity(quantity, nameof(quantity));

            InventoryStockAdjustment adjustment = CalculateStockAdjustment(
                inventoryItemId,
                currentStock,
                quantity);

            return new InventoryAllocationResult(
                new[] { adjustment },
                costSnapshot: null);
        }

        private static InventoryStockAdjustment CalculateStockAdjustment(
            Guid inventoryItemId,
            int? currentStock,
            int stockDelta)
        {
            int validatedStock = ValidateStoredStock(currentStock);
            int resultingStock;
            try
            {
                resultingStock = checked(validatedStock + stockDelta);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "The resulting inventory stock is outside the supported range.",
                    exception);
            }

            if (resultingStock < 0)
            {
                throw new InvalidOperationException(
                    "The Inventory item does not have enough stock for this Repair Part quantity.");
            }

            return new InventoryStockAdjustment(
                inventoryItemId,
                validatedStock,
                stockDelta,
                resultingStock);
        }

        private static InventoryCostSnapshot CalculateCostSnapshot(
            decimal? inventoryUnitCost,
            int quantity)
        {
            if (!inventoryUnitCost.HasValue)
            {
                throw new InvalidOperationException(
                    "The Inventory item does not contain a Unit Cost.");
            }

            if (inventoryUnitCost.Value < 0m)
            {
                throw new InvalidOperationException(
                    "The Inventory item Unit Cost cannot be negative.");
            }

            decimal totalCost;
            try
            {
                totalCost = checked(inventoryUnitCost.Value * quantity);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "The calculated Repair Part Total Cost is outside the supported range.",
                    exception);
            }

            return new InventoryCostSnapshot(inventoryUnitCost.Value, totalCost);
        }

        private static int? GetCurrentStock(
            IReadOnlyDictionary<Guid, int?> currentStockByInventoryItem,
            Guid inventoryItemId)
        {
            int? currentStock;
            if (!currentStockByInventoryItem.TryGetValue(inventoryItemId, out currentStock))
            {
                throw new InvalidOperationException(
                    "Current stock was not supplied for an affected Inventory item.");
            }

            return currentStock;
        }

        private static int ValidateStoredStock(int? currentStock)
        {
            if (!currentStock.HasValue)
            {
                throw new InvalidOperationException(
                    "The Inventory item does not contain a valid Stock Quantity.");
            }

            if (currentStock.Value < 0)
            {
                throw new InvalidOperationException(
                    "The Inventory item Stock Quantity cannot be negative.");
            }

            return currentStock.Value;
        }

        private static void ValidateInventoryItemId(Guid inventoryItemId, string parameterName)
        {
            if (inventoryItemId == Guid.Empty)
            {
                throw new ArgumentException(
                    "An Inventory item identifier is required.",
                    parameterName);
            }
        }

        private static void ValidateQuantity(int quantity, string parameterName)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Repair Part quantity must be greater than zero.");
            }
        }
    }

    internal sealed class InventoryAllocationResult
    {
        internal InventoryAllocationResult(
            IReadOnlyList<InventoryStockAdjustment> stockAdjustments,
            InventoryCostSnapshot costSnapshot)
        {
            if (stockAdjustments == null)
            {
                throw new ArgumentNullException(nameof(stockAdjustments));
            }

            StockAdjustments = new List<InventoryStockAdjustment>(
                stockAdjustments).AsReadOnly();
            CostSnapshot = costSnapshot;
        }

        internal IReadOnlyList<InventoryStockAdjustment> StockAdjustments { get; }

        internal InventoryCostSnapshot CostSnapshot { get; }
    }

    internal sealed class InventoryStockAdjustment
    {
        internal InventoryStockAdjustment(
            Guid inventoryItemId,
            int currentStock,
            int stockDelta,
            int resultingStock)
        {
            InventoryItemId = inventoryItemId;
            CurrentStock = currentStock;
            StockDelta = stockDelta;
            ResultingStock = resultingStock;
        }

        internal Guid InventoryItemId { get; }

        internal int CurrentStock { get; }

        internal int StockDelta { get; }

        internal int ResultingStock { get; }
    }

    internal sealed class InventoryCostSnapshot
    {
        internal InventoryCostSnapshot(decimal unitCost, decimal totalCost)
        {
            UnitCost = unitCost;
            TotalCost = totalCost;
        }

        internal decimal UnitCost { get; }

        internal decimal TotalCost { get; }
    }
}
