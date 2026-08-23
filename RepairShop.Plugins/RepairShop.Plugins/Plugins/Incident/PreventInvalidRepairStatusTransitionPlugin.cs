using System;
using Microsoft.Xrm.Sdk;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Services;

namespace RepairShop.Plugins.Plugins.Incident
{
    /// <summary>
    /// Rejects invalid Case Repair Status transitions before the main database transaction.
    /// Register on Update/incident, PreValidation, synchronous, filtered by Repair Status.
    /// </summary>
    public sealed class PreventInvalidRepairStatusTransitionPlugin : PluginBase
    {
        public const string PreImageAlias = "PreImage";

        private const int PreValidationStage = 10;
        private const string UpdateMessage = "Update";

        protected override string UnexpectedErrorMessage =>
            "The Repair Status transition could not be validated.";

        protected override void ExecutePlugin(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            if (!string.Equals(context.MessageName, UpdateMessage, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    context.PrimaryEntityName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase) ||
                context.Stage != PreValidationStage)
            {
                tracingService.Trace(
                    "Execution skipped: expected Update/incident/PreValidation; received {0}/{1}/stage {2}.",
                    context.MessageName,
                    context.PrimaryEntityName,
                    context.Stage);
                return;
            }

            Entity target;
            if (!TryGetTarget(context, out target) ||
                !string.Equals(
                    target.LogicalName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("Execution skipped: Target was missing or invalid.");
                return;
            }

            if (!target.Contains(IncidentSchema.RepairStatus))
            {
                tracingService.Trace("Execution skipped: Repair Status was not present in Target.");
                return;
            }

            OptionSetValue requestedStatusOption =
                target.GetAttributeValue<OptionSetValue>(IncidentSchema.RepairStatus);
            if (requestedStatusOption == null)
            {
                tracingService.Trace("Validation failed: requested Repair Status was missing or invalid.");
                throw new InvalidPluginExecutionException(
                    "Select a valid Repair Status before saving the Case.");
            }

            Entity preImage = GetRequiredPreImage(context);
            OptionSetValue previousStatusOption =
                preImage.GetAttributeValue<OptionSetValue>(IncidentSchema.RepairStatus);
            if (previousStatusOption == null)
            {
                tracingService.Trace(
                    "Validation failed: PreImage did not contain a valid Repair Status.");
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The plug-in Pre Image must contain the {0} attribute.",
                        IncidentSchema.RepairStatus));
            }

            int previousStatus = previousStatusOption.Value;
            int requestedStatus = requestedStatusOption.Value;
            tracingService.Trace(
                "Validating Repair Status transition. Previous={0}; Requested={1}.",
                previousStatus,
                requestedStatus);

            if (!RepairStatusTransitionValidator.IsKnownStatus(previousStatus) ||
                !RepairStatusTransitionValidator.IsKnownStatus(requestedStatus))
            {
                tracingService.Trace(
                    "Repair Status transition validation result: rejected (unsupported Choice value). Previous={0}; Requested={1}.",
                    previousStatus,
                    requestedStatus);
                throw new InvalidPluginExecutionException(
                    "The Case contains an unsupported Repair Status. Select a valid Repair Status and try again.");
            }

            if (previousStatus == requestedStatus)
            {
                tracingService.Trace(
                    "Repair Status transition validation result: allowed (same value). Previous={0}; Requested={1}.",
                    previousStatus,
                    requestedStatus);
                return;
            }

            bool isAllowed = RepairStatusTransitionValidator.IsTransitionAllowed(
                previousStatus,
                requestedStatus);
            tracingService.Trace(
                "Repair Status transition validation result: {0}. Previous={1}; Requested={2}.",
                isAllowed ? "allowed" : "rejected",
                previousStatus,
                requestedStatus);

            if (!isAllowed)
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "Invalid Repair Status transition: {0} cannot be changed directly to {1}.",
                        RepairStatusTransitionValidator.GetStatusName(previousStatus),
                        RepairStatusTransitionValidator.GetStatusName(requestedStatus)));
            }
        }

        private static Entity GetRequiredPreImage(IPluginExecutionContext context)
        {
            Entity preImage;
            if (context.PreEntityImages == null ||
                !context.PreEntityImages.TryGetValue(PreImageAlias, out preImage) ||
                preImage == null)
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The plug-in step is missing the required Pre Image with alias '{0}'.",
                        PreImageAlias));
            }

            return preImage;
        }
    }
}
