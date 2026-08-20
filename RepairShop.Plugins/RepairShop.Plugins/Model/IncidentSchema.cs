namespace RepairShop.Plugins.Model
{
    /// <summary>
    /// Logical names used by the Case repair feature.
    /// Replace the crs_ names if the Dataverse solution uses a different publisher prefix.
    /// In a larger solution, generate these constants from Dataverse metadata.
    /// </summary>
    internal static class IncidentSchema
    {
        internal const string EntityLogicalName = "incident";

        internal const string RepairStatus = "crs_repairstatus";
        internal const string CompletionDate = "crs_completiondate";
        internal const string EstimatedCost = "crs_estimatedcost";
        internal const string FinalCost = "crs_finalcost";
        internal const string DateReceived = "crs_datereceived";

        // Configure this Dataverse column as Whole Number with the Duration format.
        // Dataverse duration values are represented as a whole number of minutes.
        internal const string RepairDuration = "crs_repairduration";
    }
}
