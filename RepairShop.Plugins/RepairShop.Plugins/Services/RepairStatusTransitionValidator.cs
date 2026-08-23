using System.Collections.Generic;
using RepairShop.Plugins.Model;

namespace RepairShop.Plugins.Services
{
    /// <summary>
    /// Defines the permitted Case Repair Status state-machine transitions.
    /// </summary>
    internal static class RepairStatusTransitionValidator
    {
        private static readonly IReadOnlyDictionary<int, string> StatusNames =
            new Dictionary<int, string>
            {
                { RepairStatusValues.Received, "Received" },
                { RepairStatusValues.Diagnosing, "Diagnosing" },
                { RepairStatusValues.WaitingForApproval, "Waiting for Approval" },
                { RepairStatusValues.RepairInProgress, "Repair in Progress" },
                { RepairStatusValues.WaitingForParts, "Waiting for Parts" },
                { RepairStatusValues.ReadyForPickup, "Ready for Pickup" },
                { RepairStatusValues.Completed, "Completed" },
                { RepairStatusValues.Cancelled, "Cancelled" }
            };

        private static readonly IReadOnlyDictionary<int, ISet<int>> AllowedDestinations =
            new Dictionary<int, ISet<int>>
            {
                {
                    RepairStatusValues.Received,
                    Destinations(RepairStatusValues.Diagnosing, RepairStatusValues.Cancelled)
                },
                {
                    RepairStatusValues.Diagnosing,
                    Destinations(RepairStatusValues.WaitingForApproval, RepairStatusValues.Cancelled)
                },
                {
                    RepairStatusValues.WaitingForApproval,
                    Destinations(RepairStatusValues.RepairInProgress, RepairStatusValues.Cancelled)
                },
                {
                    RepairStatusValues.RepairInProgress,
                    Destinations(
                        RepairStatusValues.WaitingForParts,
                        RepairStatusValues.ReadyForPickup,
                        RepairStatusValues.Cancelled)
                },
                {
                    RepairStatusValues.WaitingForParts,
                    Destinations(
                        RepairStatusValues.RepairInProgress,
                        RepairStatusValues.ReadyForPickup,
                        RepairStatusValues.Cancelled)
                },
                {
                    RepairStatusValues.ReadyForPickup,
                    Destinations(RepairStatusValues.Completed, RepairStatusValues.Cancelled)
                },
                { RepairStatusValues.Completed, Destinations() },
                { RepairStatusValues.Cancelled, Destinations() }
            };

        internal static bool IsKnownStatus(int status)
        {
            return StatusNames.ContainsKey(status);
        }

        internal static string GetStatusName(int status)
        {
            string statusName;
            return StatusNames.TryGetValue(status, out statusName)
                ? statusName
                : "Unknown";
        }

        internal static bool IsTransitionAllowed(int previousStatus, int requestedStatus)
        {
            if (!IsKnownStatus(previousStatus) || !IsKnownStatus(requestedStatus))
            {
                return false;
            }

            if (previousStatus == requestedStatus)
            {
                return true;
            }

            ISet<int> destinations;
            return AllowedDestinations.TryGetValue(previousStatus, out destinations) &&
                destinations.Contains(requestedStatus);
        }

        private static ISet<int> Destinations(params int[] statuses)
        {
            return new HashSet<int>(statuses);
        }
    }
}
