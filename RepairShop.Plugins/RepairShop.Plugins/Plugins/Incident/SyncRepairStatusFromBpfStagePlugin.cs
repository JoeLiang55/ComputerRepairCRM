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

            Guid bpfRecordId = context.PrimaryEntityId != Guid.Empty
                ? context.PrimaryEntityId
                : target.Id;
            tracingService.Trace("BPF record ID: {0}.", bpfRecordId);

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
                tracingService.Trace(
                    "Execution skipped: PostImage attribute {0} was missing or invalid.",
                    ComputerRepairProcessSchema.ActiveStage);
                return;
            }

            tracingService.Trace(
                "Active stage ID: {0}. Resolving stage name from {1}.{2}; EntityReference.Name is not used.",
                activeStage.Id,
                ComputerRepairProcessSchema.ProcessStageEntityLogicalName,
                ComputerRepairProcessSchema.ProcessStageName);

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

            if (string.IsNullOrWhiteSpace(stageName))
            {
                tracingService.Trace(
                    "Execution skipped: Process Stage {0} did not provide {1}.",
                    activeStage.Id,
                    ComputerRepairProcessSchema.ProcessStageName);
                return;
            }

            stageName = stageName.Trim();
            tracingService.Trace("Resolved active stage name: '{0}'.", stageName);

            int targetRepairStatus;
            if (!RepairStatusStageMapper.TryMap(stageName, out targetRepairStatus))
            {
                tracingService.Trace(
                    "Execution skipped: stage '{0}' has no Repair Status mapping.",
                    stageName ?? "<missing>");
                return;
            }

            tracingService.Trace("Mapped Repair Status: {0}.", targetRepairStatus);

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
                    "Execution skipped: PostImage attribute {0} did not contain a valid incident reference.",
                    ComputerRepairProcessSchema.RelatedCase);
                return;
            }

            tracingService.Trace("Related Case ID: {0}.", relatedCase.Id);

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
                    "Case update skipped: Case {0} Repair Status already equals {1}.",
                    relatedCase.Id,
                    targetRepairStatus);
                return;
            }

            var caseUpdate = new Entity(IncidentSchema.EntityLogicalName, relatedCase.Id);
            caseUpdate[IncidentSchema.RepairStatus] = new OptionSetValue(targetRepairStatus);
            organizationService.Update(caseUpdate);

            tracingService.Trace(
                "Case update executed: Case {0}, attribute {1}, value {2}, BPF stage '{3}'.",
                relatedCase.Id,
                IncidentSchema.RepairStatus,
                targetRepairStatus,
                stageName);
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
