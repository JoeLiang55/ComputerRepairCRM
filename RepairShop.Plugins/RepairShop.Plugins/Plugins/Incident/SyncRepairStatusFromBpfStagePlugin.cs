using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Services;

namespace RepairShop.Plugins.Plugins.Incident
{
    /// <summary>
    /// Synchronizes Case Repair Status after the active Computer Repair Process stage changes.
    /// Register on Update of the BPF instance table, PostOperation, synchronous, filtered by
    /// activestageid, with the Post Image documented in PLUGIN_REGISTRATION.md.
    /// </summary>
    public sealed class SyncRepairStatusFromBpfStagePlugin : PluginBase
    {
        public const string PostImageAlias = "PostImage";

        private const int PostOperationStage = 40;
        private const string UpdateMessage = "Update";

        protected override string UnexpectedErrorMessage =>
            "Repair Status could not be synchronized from the active repair stage.";

        protected override void ExecutePlugin(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            if (!string.Equals(context.MessageName, UpdateMessage, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    context.PrimaryEntityName,
                    ComputerRepairProcessSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase) ||
                context.Stage != PostOperationStage)
            {
                tracingService.Trace(
                    "Execution skipped: expected Update/{0}/PostOperation; received {1}/{2}/stage {3}.",
                    ComputerRepairProcessSchema.EntityLogicalName,
                    context.MessageName,
                    context.PrimaryEntityName,
                    context.Stage);
                return;
            }

            Entity target;
            if (!TryGetTarget(context, out target) ||
                !string.Equals(
                    target.LogicalName,
                    ComputerRepairProcessSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("Execution skipped: Target was missing or invalid.");
                return;
            }

            if (!target.Contains(ComputerRepairProcessSchema.ActiveStage))
            {
                tracingService.Trace("Execution skipped: active stage was not present in Target.");
                return;
            }

            Entity postImage;
            if (context.PostEntityImages == null ||
                !context.PostEntityImages.TryGetValue(PostImageAlias, out postImage) ||
                postImage == null)
            {
                tracingService.Trace(
                    "Execution skipped: required Post Image '{0}' was missing.",
                    PostImageAlias);
                return;
            }

            EntityReference activeStage = postImage.GetAttributeValue<EntityReference>(
                ComputerRepairProcessSchema.ActiveStage);
            if (activeStage == null || activeStage.Id == Guid.Empty)
            {
                tracingService.Trace("Execution skipped: current active stage was missing or invalid.");
                return;
            }

            EntityReference relatedCase = postImage.GetAttributeValue<EntityReference>(
                ComputerRepairProcessSchema.RelatedCase);
            if (relatedCase == null ||
                relatedCase.Id == Guid.Empty ||
                !string.Equals(
                    relatedCase.LogicalName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace(
                    "Execution skipped: BPF instance had no valid related Case in {0}.",
                    ComputerRepairProcessSchema.RelatedCase);
                return;
            }

            IOrganizationService organizationService = GetOrganizationService(
                serviceProvider,
                context.UserId);

            Entity processStage = organizationService.Retrieve(
                ComputerRepairProcessSchema.ProcessStageEntityLogicalName,
                activeStage.Id,
                new ColumnSet(ComputerRepairProcessSchema.ProcessStageName));
            string stageName = processStage == null
                ? null
                : processStage.GetAttributeValue<string>(ComputerRepairProcessSchema.ProcessStageName);

            int targetRepairStatus;
            if (!RepairStatusStageMapper.TryMap(stageName, out targetRepairStatus))
            {
                tracingService.Trace(
                    "Execution skipped: stage '{0}' has no Repair Status mapping.",
                    stageName ?? "<missing>");
                return;
            }

            Entity currentCase = organizationService.Retrieve(
                IncidentSchema.EntityLogicalName,
                relatedCase.Id,
                new ColumnSet(IncidentSchema.RepairStatus));
            OptionSetValue currentRepairStatus = currentCase == null
                ? null
                : currentCase.GetAttributeValue<OptionSetValue>(IncidentSchema.RepairStatus);

            if (currentRepairStatus != null && currentRepairStatus.Value == targetRepairStatus)
            {
                tracingService.Trace(
                    "Case update skipped: Repair Status already matches the active stage.");
                return;
            }

            var caseUpdate = new Entity(IncidentSchema.EntityLogicalName, relatedCase.Id);
            caseUpdate[IncidentSchema.RepairStatus] = new OptionSetValue(targetRepairStatus);
            organizationService.Update(caseUpdate);

            tracingService.Trace(
                "Case {0} Repair Status set to {1} for BPF stage '{2}'.",
                relatedCase.Id,
                targetRepairStatus,
                stageName.Trim());
        }

        private static IOrganizationService GetOrganizationService(
            IServiceProvider serviceProvider,
            Guid userId)
        {
            var factory =
                (IOrganizationServiceFactory)serviceProvider.GetService(
                    typeof(IOrganizationServiceFactory));
            if (factory == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Dataverse organization service factory is unavailable.");
            }

            IOrganizationService service = factory.CreateOrganizationService(userId);
            if (service == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Dataverse organization service is unavailable.");
            }

            return service;
        }
    }
}
