namespace RepairShop.Plugins.Model
{
    /// <summary>
    /// Logical names used to synchronize the Computer Repair Process BPF to its Case.
    /// Confirmed logical names for the Computer Repair Process BPF integration.
    /// </summary>
    internal static class ComputerRepairProcessSchema
    {
        internal const string EntityLogicalName = "gsic_computerrepairprocess";
        internal const string ActiveStage = "activestageid";
        internal const string RelatedCase = "bpf_incidentid";

        internal const string ProcessStageEntityLogicalName = "processstage";
        internal const string ProcessStageName = "stagename";
    }
}
