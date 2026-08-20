using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using RepairShop.Plugins.Model;

namespace RepairShop.Plugins.Plugins.Incident
{
    /// <summary>
    /// Synchronizes Case Repair Status when the active Computer Repair Process stage changes.
    ///
    /// Register on Update of gsic_computerrepairprocess, PostOperation, synchronous,
    /// filtered by activestageid.
    /// </summary>
    public sealed class SyncRepairStatusFromBpfStagePlugin : IPlugin
    {
        private const int PostOperationStage = 40;
        private const string UpdateMessage = "Update";

        private static readonly IDictionary<string, int> RepairStatusByStageName =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Received", 702670000 },
                { "Diagnosing", 702670001 },
                { "Waiting For Approval", 702670002 },
                { "Repair In Progress", 702670003 },
                { "Ready For Pickup", 702670005 }
            };

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var tracingService =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (tracingService == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Dataverse tracing service is unavailable.");
            }

            if (context == null)
            {
                tracingService.Trace("Execution stopped: plug-in context is unavailable.");
                throw new InvalidPluginExecutionException(
                    "The Dataverse plug-in execution context is unavailable.");
            }

            tracingService.Trace(
                "{0}: Start. CorrelationId={1}, OperationId={2}, Depth={3}.",
                nameof(SyncRepairStatusFromBpfStagePlugin),
                context.CorrelationId,
                context.OperationId,
                context.Depth);

            try
            {
                var serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(
                        typeof(IOrganizationServiceFactory));

                if (serviceFactory == null)
                {
                    throw new InvalidPluginExecutionException(
                        "The Dataverse organization service factory is unavailable.");
                }

                IOrganizationService organizationService =
                    serviceFactory.CreateOrganizationService(context.UserId);

                if (organizationService == null)
                {
                    throw new InvalidPluginExecutionException(
                        "The Dataverse organization service is unavailable.");
                }

                ExecuteCore(context, organizationService, tracingService);

                tracingService.Trace(
                    "{0}: Completed successfully.",
                    nameof(SyncRepairStatusFromBpfStagePlugin));
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace(
                    "{0}: Execution error: {1}",
                    nameof(SyncRepairStatusFromBpfStagePlugin),
                    ex.ToString());
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace(
                    "{0}: Unexpected error: {1}",
                    nameof(SyncRepairStatusFromBpfStagePlugin),
                    ex.ToString());

                throw new InvalidPluginExecutionException(
                    string.Format(
                        "Repair Status could not be synchronized from the active repair stage. " +
                        "Contact an administrator and provide correlation ID {0}.",
                        context.CorrelationId),
                    ex);
            }
        }

        private static void ExecuteCore(
            IPluginExecutionContext context,
            IOrganizationService organizationService,
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
            if (!TryGetTarget(context, out target))
            {
                tracingService.Trace(
                    "Execution skipped: InputParameters did not contain an Entity Target.");
                return;
            }

            if (!target.Contains(ComputerRepairProcessSchema.ActiveStage))
            {
                tracingService.Trace(
                    "Execution skipped: Target did not contain {0}.",
                    ComputerRepairProcessSchema.ActiveStage);
                return;
            }

            EntityReference activeStage =
                target.GetAttributeValue<EntityReference>(ComputerRepairProcessSchema.ActiveStage);

            if (activeStage == null || activeStage.Id == Guid.Empty)
            {
                tracingService.Trace("Execution skipped: active stage was null or invalid.");
                return;
            }

            tracingService.Trace("Active stage ID: {0}.", activeStage.Id);

            Entity processStage = organizationService.Retrieve(
                ComputerRepairProcessSchema.ProcessStageEntityLogicalName,
                activeStage.Id,
                new ColumnSet(ComputerRepairProcessSchema.ProcessStageName));

            string stageName = processStage.GetAttributeValue<string>(
                ComputerRepairProcessSchema.ProcessStageName);

            if (string.IsNullOrWhiteSpace(stageName))
            {
                tracingService.Trace("Execution skipped: process stage name was null or empty.");
                return;
            }

            stageName = stageName.Trim();
            tracingService.Trace("Stage name: {0}.", stageName);

            Guid bpfInstanceId = context.PrimaryEntityId;
            if (bpfInstanceId == Guid.Empty)
            {
                tracingService.Trace("Execution skipped: BPF instance ID was empty.");
                return;
            }

            tracingService.Trace("BPF instance ID: {0}.", bpfInstanceId);

            Entity bpfInstance = organizationService.Retrieve(
                ComputerRepairProcessSchema.EntityLogicalName,
                bpfInstanceId,
                new ColumnSet(ComputerRepairProcessSchema.RelatedCase));

            EntityReference relatedCase = bpfInstance.GetAttributeValue<EntityReference>(
                ComputerRepairProcessSchema.RelatedCase);

            if (relatedCase == null ||
                relatedCase.Id == Guid.Empty ||
                !string.Equals(
                    relatedCase.LogicalName,
                    ComputerRepairProcessSchema.CaseEntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace(
                    "Execution skipped: BPF instance had no valid related Case in {0}.",
                    ComputerRepairProcessSchema.RelatedCase);
                return;
            }

            tracingService.Trace("Related Case ID: {0}.", relatedCase.Id);

            int targetRepairStatus;
            if (!RepairStatusByStageName.TryGetValue(stageName, out targetRepairStatus))
            {
                tracingService.Trace(
                    "Execution skipped: stage '{0}' has no Repair Status mapping.",
                    stageName);
                return;
            }

            tracingService.Trace("Target Repair Status: {0}.", targetRepairStatus);

            Entity currentCase = organizationService.Retrieve(
                ComputerRepairProcessSchema.CaseEntityLogicalName,
                relatedCase.Id,
                new ColumnSet(ComputerRepairProcessSchema.CaseRepairStatus));

            OptionSetValue currentRepairStatus = currentCase.GetAttributeValue<OptionSetValue>(
                ComputerRepairProcessSchema.CaseRepairStatus);

            tracingService.Trace(
                "Current Repair Status: {0}.",
                currentRepairStatus == null ? "null" : currentRepairStatus.Value.ToString());

            if (currentRepairStatus != null && currentRepairStatus.Value == targetRepairStatus)
            {
                tracingService.Trace(
                    "Case update skipped: Repair Status already matches the active stage.");
                return;
            }

            var caseUpdate = new Entity(
                ComputerRepairProcessSchema.CaseEntityLogicalName,
                relatedCase.Id);
            caseUpdate[ComputerRepairProcessSchema.CaseRepairStatus] =
                new OptionSetValue(targetRepairStatus);

            organizationService.Update(caseUpdate);

            tracingService.Trace(
                "Case update executed: {0} set to {1} on Case {2}.",
                ComputerRepairProcessSchema.CaseRepairStatus,
                targetRepairStatus,
                relatedCase.Id);
        }

        private static bool TryGetTarget(
            IPluginExecutionContext context,
            out Entity target)
        {
            target = null;

            if (!context.InputParameters.Contains("Target"))
            {
                return false;
            }

            target = context.InputParameters["Target"] as Entity;
            return target != null;
        }
    }
}
