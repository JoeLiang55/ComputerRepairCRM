using System;
using Microsoft.Xrm.Sdk;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Services;

namespace RepairShop.Plugins.Plugins.Incident
{
    /// <summary>
    /// Adds completion values to the incoming Case update when Repair Status becomes Completed.
    /// Register on Update/incident, PreOperation, synchronous, filtered by Repair Status.
    /// </summary>
    public sealed class CompleteRepairOnStatusChangePlugin : PluginBase
    {
        public const string PreImageAlias = "PreImage";

        private const int PreOperationStage = 20;
        private const string UpdateMessage = "Update";

        protected override string UnexpectedErrorMessage =>
            "The repair completion update failed.";

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
                context.Stage != PreOperationStage)
            {
                tracingService.Trace(
                    "Execution skipped: expected Update/incident/PreOperation; received {0}/{1}/stage {2}.",
                    context.MessageName,
                    context.PrimaryEntityName,
                    context.Stage);
                return;
            }

            Entity target;
            if (!TryGetTarget(context, out target))
            {
                tracingService.Trace("Execution skipped: Target was missing or was not an Entity.");
                return;
            }

            if (!string.Equals(
                    target.LogicalName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("Execution skipped: Target logical name was {0}.", target.LogicalName);
                return;
            }

            if (target.Id == Guid.Empty)
            {
                throw new InvalidPluginExecutionException(
                    "The Case update does not contain a valid record identifier.");
            }

            if (!target.Contains(IncidentSchema.RepairStatus))
            {
                tracingService.Trace("Execution skipped: Repair Status was not present in Target.");
                return;
            }

            int completedStatusValue = RepairStatusValues.Completed;
            if (!RepairCompletionService.IsCompletedStatus(target, completedStatusValue))
            {
                tracingService.Trace(
                    "Execution skipped: new Repair Status is not Completed ({0}).",
                    completedStatusValue);
                return;
            }

            Entity preImage = GetRequiredPreImage(context);
            if (!RepairCompletionService.IsTransitionToCompleted(
                    target,
                    preImage,
                    completedStatusValue))
            {
                tracingService.Trace(
                    "Execution skipped: Repair Status did not transition to Completed ({0}).",
                    completedStatusValue);
                return;
            }

            Money effectiveFinalCost = target.Contains(IncidentSchema.FinalCost)
                ? target.GetAttributeValue<Money>(IncidentSchema.FinalCost)
                : preImage.GetAttributeValue<Money>(IncidentSchema.FinalCost);

            int durationMinutes = RepairCompletionService.ApplyCompletion(
                target,
                preImage,
                DateTime.UtcNow);

            tracingService.Trace(
                effectiveFinalCost == null
                    ? "Final Cost was empty; Estimated Cost was copied when available."
                    : "Final Cost already had a value and was preserved.");
            tracingService.Trace(
                "Case {0} completion values added to Target. Duration={1} minute(s).",
                target.Id,
                durationMinutes);
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
