namespace RepairShop.Plugins.Model
{
    /// <summary>
    /// Confirmed integer values for the Case Repair Status Choice column.
    /// </summary>
    internal static class RepairStatusValues
    {
        internal const int Received = 702670000;
        internal const int Diagnosing = 702670001;
        internal const int WaitingForApproval = 702670002;
        internal const int RepairInProgress = 702670003;
        internal const int WaitingForParts = 702670004;
        internal const int ReadyForPickup = 702670005;
        internal const int Completed = 702670006;
        internal const int Cancelled = 702670007;
    }
}
