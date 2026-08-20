namespace RepairShop.Plugins.Model
{
    /// <summary>
    /// Logical names used to synchronize the Computer Repair Process BPF to its Case.
    /// </summary>
    internal static class ComputerRepairProcessSchema
    {
        internal const string EntityLogicalName = "gsic_computerrepairprocess";
        internal const string ActiveStage = "activestageid";

        // Generated BPF relationship columns normally follow bpf_<primarytable>id.
        // Confirm this logical name in the Computer Repair Process table metadata.
        internal const string RelatedCase = "bpf_incidentid";

        internal const string ProcessStageEntityLogicalName = "processstage";
        internal const string ProcessStageName = "stagename";

        internal const string CaseEntityLogicalName = "incident";
        internal const string CaseRepairStatus = "cr1a3_repairstatus";
    }
}
