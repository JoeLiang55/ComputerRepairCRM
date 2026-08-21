namespace RepairShop.Plugins.Model
{
    /// <summary>
    /// Logical names used to synchronize the Computer Repair Process BPF to its Case.
    /// The BPF table is confirmed; the related-Case lookup remains to be verified.
    /// </summary>
    internal static class ComputerRepairProcessSchema
    {
        // Confirmed from the live Dataverse environment.
        internal const string EntityLogicalName = "gsic_computerrepairprocess";
        // Not yet independently verified in repository or live metadata.
        internal const string ActiveStage = "activestageid";

        // Unverified custom metadata; generated BPF relationship columns commonly follow
        // bpf_<primarytable>id, but the actual logical name must come from Dataverse.
        internal const string RelatedCase = "bpf_incidentid";

        internal const string ProcessStageEntityLogicalName = "processstage";
        internal const string ProcessStageName = "stagename";

    }
}
