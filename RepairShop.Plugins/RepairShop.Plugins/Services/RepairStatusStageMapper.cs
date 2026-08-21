using System;
using System.Collections.Generic;
using RepairShop.Plugins.Model;

namespace RepairShop.Plugins.Services
{
    internal static class RepairStatusStageMapper
    {
        private static readonly IReadOnlyDictionary<string, int> RepairStatusByStageName =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Received", RepairStatusValues.Received },
                { "Diagnosing", RepairStatusValues.Diagnosing },
                { "Waiting For Approval", RepairStatusValues.WaitingForApproval },
                { "Repair In Progress", RepairStatusValues.RepairInProgress },
                { "Ready For Pickup", RepairStatusValues.ReadyForPickup }
            };

        internal static bool TryMap(string stageName, out int repairStatus)
        {
            repairStatus = default(int);
            return !string.IsNullOrWhiteSpace(stageName) &&
                RepairStatusByStageName.TryGetValue(stageName.Trim(), out repairStatus);
        }
    }
}
