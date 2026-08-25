namespace RepairShop.Plugins.Model
{
    /// <summary>
    /// Logical names used by the Case repair feature.
    /// In a larger solution, generate these constants from Dataverse metadata.
    /// </summary>
    internal static class IncidentSchema
    {
        internal const string EntityLogicalName = "incident";

        // Confirmed schema name: cr1a3_RepairStatus. Dataverse SDK attribute logical
        // names are lowercase, which also matches the existing project metadata convention.
        internal const string RepairStatus = "cr1a3_repairstatus";
        internal const string CompletionDate = "gsic_completiondate";

        internal const string EstimatedCost = "cr1a3_estimatedcost";
        internal const string FinalCost = "cr1a3_finalcost";
        internal const string DateReceived = "cr1a3_datereceived";

        // Standard Dataverse Case Priority (incident.prioritycode). The default
        // Choice values are defined in IncidentPriorityValues.
        internal const string Priority = "prioritycode";

        internal const string UnresolvedRepairDueDate =
            "__UNRESOLVED_REPAIR_DUE_DATE_LOGICAL_NAME__";

        // Confirmed logical name for the Repair Due Date column.
        internal const string RepairDueDate = "gsic_repairduedate";

        // Dataverse Whole Number / Duration columns store whole minutes.
        internal const string RepairDuration = "gsic_repairduration";
    }
}
