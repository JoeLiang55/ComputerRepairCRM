using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using RepairShop.Plugins.Model;

namespace RepairShop.Plugins.Plugins.SlaKpiInstance
{
    /// <summary>
    /// Escalates the active repair Case when its current SLA KPI instance nears or reaches failure.
    /// Register asynchronously on Update/slakpiinstance, PostOperation, filtered by status, with
    /// the Pre and Post Images documented in PLUGIN_REGISTRATION.md.
    /// </summary>
    public sealed class EscalateRepairOnSlaStatusPlugin : PluginBase
    {
        public const string PreImageAlias = "PreImage";
        public const string PostImageAlias = "PostImage";

        private const int PostOperationStage = 40;
        private const string UpdateMessage = "Update";

        protected override string UnexpectedErrorMessage =>
            "The repair Case could not be escalated from its SLA status.";

        protected override void ExecutePlugin(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            if (!string.Equals(context.MessageName, UpdateMessage, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    context.PrimaryEntityName,
                    SlaKpiInstanceSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase) ||
                context.Stage != PostOperationStage)
            {
                tracingService.Trace(
                    "Execution skipped: expected Update/{0}/PostOperation; received {1}/{2}/stage {3}.",
                    SlaKpiInstanceSchema.EntityLogicalName,
                    context.MessageName,
                    context.PrimaryEntityName,
                    context.Stage);
                return;
            }

            Entity target;
            if (!TryGetTarget(context, out target) ||
                !string.Equals(
                    target.LogicalName,
                    SlaKpiInstanceSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("Execution skipped: Target was missing or invalid.");
                return;
            }

            Guid slaKpiInstanceId = context.PrimaryEntityId != Guid.Empty
                ? context.PrimaryEntityId
                : target.Id;
            if (slaKpiInstanceId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException(
                    "The SLA KPI Instance update does not contain a valid record identifier.");
            }

            Entity preImage = GetRequiredImage(
                context.PreEntityImages,
                PreImageAlias,
                "Pre");
            Entity postImage = GetRequiredImage(
                context.PostEntityImages,
                PostImageAlias,
                "Post");

            int previousStatus = GetRequiredStatus(preImage, PreImageAlias);
            int currentStatus = GetRequiredStatus(postImage, PostImageAlias);
            tracingService.Trace(
                "SLA KPI Instance {0} status transition: {1} -> {2}.",
                slaKpiInstanceId,
                previousStatus,
                currentStatus);

            if (previousStatus == currentStatus)
            {
                tracingService.Trace("Execution skipped: SLA KPI status did not change.");
                return;
            }

            if (currentStatus != SlaKpiInstanceStatusValues.NearingNoncompliance &&
                currentStatus != SlaKpiInstanceStatusValues.Noncompliant)
            {
                tracingService.Trace(
                    "Execution skipped: new SLA KPI status {0} is not an escalation status.",
                    currentStatus);
                return;
            }

            IOrganizationService organizationService = GetOrganizationService(
                serviceProvider,
                context.UserId);
            Entity repairCase = FindCurrentRepairCase(organizationService, slaKpiInstanceId);
            if (repairCase == null)
            {
                tracingService.Trace(
                    "Execution skipped: no Case currently references SLA KPI Instance {0} through {1}.",
                    slaKpiInstanceId,
                    IncidentSchema.SlaKpiInstance);
                return;
            }

            OptionSetValue repairStatus = repairCase.GetAttributeValue<OptionSetValue>(
                IncidentSchema.RepairStatus);
            if (repairStatus != null &&
                (repairStatus.Value == RepairStatusValues.Completed ||
                 repairStatus.Value == RepairStatusValues.Cancelled))
            {
                tracingService.Trace(
                    "Case {0} escalation skipped: Repair Status {1} is terminal.",
                    repairCase.Id,
                    repairStatus.Value);
                return;
            }

            if (repairCase.GetAttributeValue<bool>(IncidentSchema.IsEscalated))
            {
                tracingService.Trace(
                    "Case {0} escalation skipped: {1} is already true.",
                    repairCase.Id,
                    IncidentSchema.IsEscalated);
                return;
            }

            var caseUpdate = new Entity(IncidentSchema.EntityLogicalName, repairCase.Id);
            caseUpdate[IncidentSchema.IsEscalated] = true;
            organizationService.Update(caseUpdate);

            tracingService.Trace(
                "Case {0} escalated from SLA KPI Instance {1}; sparse update set only {2}.",
                repairCase.Id,
                slaKpiInstanceId,
                IncidentSchema.IsEscalated);
        }

        private static Entity GetRequiredImage(
            EntityImageCollection images,
            string alias,
            string imageType)
        {
            Entity image;
            if (images == null ||
                !images.TryGetValue(alias, out image) ||
                image == null)
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The required {0} Image '{1}' is missing. Verify the plug-in step registration.",
                        imageType,
                        alias));
            }

            return image;
        }

        private static int GetRequiredStatus(Entity image, string alias)
        {
            OptionSetValue status = image.GetAttributeValue<OptionSetValue>(
                SlaKpiInstanceSchema.Status);
            if (status == null)
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The required image '{0}' does not contain {1}. Verify the plug-in image columns.",
                        alias,
                        SlaKpiInstanceSchema.Status));
            }

            return status.Value;
        }

        private static Entity FindCurrentRepairCase(
            IOrganizationService organizationService,
            Guid slaKpiInstanceId)
        {
            var query = new QueryExpression(IncidentSchema.EntityLogicalName)
            {
                ColumnSet = new ColumnSet(
                    IncidentSchema.RepairStatus,
                    IncidentSchema.IsEscalated),
                TopCount = 1
            };
            query.Criteria.AddCondition(
                IncidentSchema.SlaKpiInstance,
                ConditionOperator.Equal,
                slaKpiInstanceId);

            EntityCollection results = organizationService.RetrieveMultiple(query);
            return results != null && results.Entities.Count > 0
                ? results.Entities[0]
                : null;
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
