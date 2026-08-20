using System;
using Microsoft.Xrm.Sdk;
using RepairShop.Plugins.Configuration;
using RepairShop.Plugins.Model;

namespace RepairShop.Plugins.Plugins.Incident
{
    /// <summary>
    /// Adds the repair completion values to the original Case update when Repair Status
    /// transitions to Completed.
    ///
    /// Register on Update of incident, PreOperation, synchronous, filtered by Repair Status.
    /// Register the Pre Image described in README.md.
    /// </summary>
    public sealed class CompleteRepairOnStatusChangePlugin : IPlugin
    {
        public const string PreImageAlias = "PreImage";

        private const int PreOperationStage = 20;
        private const string UpdateMessage = "Update";

        // Dataverse can cache and concurrently invoke a plug-in instance. This field contains
        // immutable registration configuration only; all invocation state remains local.
        private readonly PluginConfiguration configuration;

        public CompleteRepairOnStatusChangePlugin()
            : this(null)
        {
        }

        public CompleteRepairOnStatusChangePlugin(string unsecureConfiguration)
        {
            configuration = new PluginConfiguration(unsecureConfiguration);
        }

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
                nameof(CompleteRepairOnStatusChangePlugin),
                context.CorrelationId,
                context.OperationId,
                context.Depth);

            try
            {
                ExecuteCore(context, tracingService);

                tracingService.Trace(
                    "{0}: Completed successfully.",
                    nameof(CompleteRepairOnStatusChangePlugin));
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace(
                    "{0}: Business/configuration error: {1}",
                    nameof(CompleteRepairOnStatusChangePlugin),
                    ex.ToString());
                throw;
            }
            catch (Exception ex)
            {
                // Keep technical detail in the trace and give the app user a stable message.
                tracingService.Trace(
                    "{0}: Unexpected error: {1}",
                    nameof(CompleteRepairOnStatusChangePlugin),
                    ex.ToString());

                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The repair completion update failed. Contact an administrator and provide correlation ID {0}.",
                        context.CorrelationId),
                    ex);
            }
        }

        private void ExecuteCore(
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            // Defensive registration checks make accidental step misconfiguration harmless.
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
                tracingService.Trace("Execution skipped: InputParameters did not contain an Entity Target.");
                return;
            }

            if (!string.Equals(
                    target.LogicalName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace(
                    "Execution skipped: Target logical name was {0}.",
                    target.LogicalName);
                return;
            }

            if (target.Id == Guid.Empty)
            {
                throw new InvalidPluginExecutionException(
                    "The Case update does not contain a valid record identifier.");
            }

            // Filtering attributes only prove that the column was submitted. Target plus the
            // Pre Image comparison below proves that the Choice value truly changed.
            if (!target.Contains(IncidentSchema.RepairStatus))
            {
                tracingService.Trace("Execution skipped: Repair Status was not present in Target.");
                return;
            }

            int completedStatusValue = GetCompletedStatusValue();
            OptionSetValue newRepairStatus =
                target.GetAttributeValue<OptionSetValue>(IncidentSchema.RepairStatus);

            if (newRepairStatus == null || newRepairStatus.Value != completedStatusValue)
            {
                tracingService.Trace(
                    "Execution skipped: new Repair Status ({0}) is not Completed ({1}).",
                    newRepairStatus == null ? "null" : newRepairStatus.Value.ToString(),
                    completedStatusValue);
                return;
            }

            Entity preImage = GetRequiredPreImage(context);
            OptionSetValue previousRepairStatus =
                preImage.GetAttributeValue<OptionSetValue>(IncidentSchema.RepairStatus);

            if (previousRepairStatus != null && previousRepairStatus.Value == completedStatusValue)
            {
                tracingService.Trace(
                    "Execution skipped: Repair Status was already Completed; no transition occurred.");
                return;
            }

            DateTime completionDateUtc = DateTime.UtcNow;
            DateTime dateReceived = GetRequiredDateReceived(target, preImage);
            int repairDurationMinutes = CalculateDurationMinutes(
                dateReceived,
                completionDateUtc);

            // In PreOperation, changing Target joins these values to the original Dataverse
            // update. No IOrganizationService.Update call is needed, so there is no recursive
            // Update pipeline and no unrelated plug-in step is invoked a second time.
            SetIfDifferent(
                target,
                preImage,
                IncidentSchema.CompletionDate,
                completionDateUtc,
                tracingService);
            SetIfDifferent(
                target,
                preImage,
                IncidentSchema.RepairDuration,
                repairDurationMinutes,
                tracingService);

            Money finalCost = GetEffectiveValue<Money>(
                target,
                preImage,
                IncidentSchema.FinalCost);

            if (finalCost == null)
            {
                Money estimatedCost = GetEffectiveValue<Money>(
                    target,
                    preImage,
                    IncidentSchema.EstimatedCost);

                if (estimatedCost != null)
                {
                    target[IncidentSchema.FinalCost] = new Money(estimatedCost.Value);
                    tracingService.Trace(
                        "Final Cost is null; Estimated Cost was copied to Final Cost.");
                }
                else
                {
                    tracingService.Trace(
                        "Final Cost and Estimated Cost are both null; Final Cost remains null.");
                }
            }
            else
            {
                tracingService.Trace("Final Cost already has a value; it was not overwritten.");
            }

            tracingService.Trace(
                "Case {0} completion values added to Target. Duration={1} minute(s).",
                target.Id,
                repairDurationMinutes);
        }

        private int GetCompletedStatusValue()
        {
            if (!configuration.CompletedStatusValue.HasValue)
            {
                throw new InvalidPluginExecutionException(
                    "The plug-in step is missing valid unsecure configuration. " +
                    "Set completedStatusValue to the integer value of the Completed Repair Status choice.");
            }

            return configuration.CompletedStatusValue.Value;
        }

        private static bool TryGetTarget(IPluginExecutionContext context, out Entity target)
        {
            target = null;

            if (!context.InputParameters.Contains("Target"))
            {
                return false;
            }

            target = context.InputParameters["Target"] as Entity;
            return target != null;
        }

        private static Entity GetRequiredPreImage(IPluginExecutionContext context)
        {
            Entity preImage;
            if (!context.PreEntityImages.TryGetValue(PreImageAlias, out preImage) ||
                preImage == null)
            {
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "The plug-in step is missing the required Pre Image with alias '{0}'.",
                        PreImageAlias));
            }

            return preImage;
        }

        private static DateTime GetRequiredDateReceived(Entity target, Entity preImage)
        {
            DateTime? dateReceived = GetEffectiveNullableValue<DateTime>(
                target,
                preImage,
                IncidentSchema.DateReceived);

            if (!dateReceived.HasValue)
            {
                throw new InvalidPluginExecutionException(
                    "The Case cannot be completed because Date Received is missing.");
            }

            return dateReceived.Value;
        }

        private static int CalculateDurationMinutes(
            DateTime dateReceived,
            DateTime completionDateUtc)
        {
            // SDK values for a User Local DateTime column are UTC. Normalize defensively for
            // unit-test doubles that may supply Local or Unspecified values.
            DateTime receivedUtc;
            if (dateReceived.Kind == DateTimeKind.Utc)
            {
                receivedUtc = dateReceived;
            }
            else if (dateReceived.Kind == DateTimeKind.Local)
            {
                receivedUtc = dateReceived.ToUniversalTime();
            }
            else
            {
                receivedUtc = DateTime.SpecifyKind(dateReceived, DateTimeKind.Utc);
            }

            double totalMinutes = (completionDateUtc - receivedUtc).TotalMinutes;
            if (totalMinutes < 0)
            {
                throw new InvalidPluginExecutionException(
                    "The Case cannot be completed because Date Received is in the future.");
            }

            if (totalMinutes > int.MaxValue)
            {
                throw new InvalidPluginExecutionException(
                    "The calculated Repair Duration is outside the supported range.");
            }

            // A Dataverse Duration column stores whole minutes. Truncation avoids recording a
            // complete minute until that minute has actually elapsed.
            return (int)Math.Floor(totalMinutes);
        }

        private static T GetEffectiveValue<T>(
            Entity target,
            Entity preImage,
            string attributeName)
            where T : class
        {
            return target.Contains(attributeName)
                ? target.GetAttributeValue<T>(attributeName)
                : preImage.GetAttributeValue<T>(attributeName);
        }

        private static T? GetEffectiveNullableValue<T>(
            Entity target,
            Entity preImage,
            string attributeName)
            where T : struct
        {
            if (target.Contains(attributeName))
            {
                return target[attributeName] == null
                    ? (T?)null
                    : target.GetAttributeValue<T>(attributeName);
            }

            return preImage.Contains(attributeName) && preImage[attributeName] != null
                ? preImage.GetAttributeValue<T>(attributeName)
                : (T?)null;
        }

        private static void SetIfDifferent<T>(
            Entity target,
            Entity preImage,
            string attributeName,
            T desiredValue,
            ITracingService tracingService)
        {
            T currentValue = target.Contains(attributeName)
                ? target.GetAttributeValue<T>(attributeName)
                : preImage.GetAttributeValue<T>(attributeName);

            if (object.Equals(currentValue, desiredValue))
            {
                tracingService.Trace(
                    "Column {0} already has the desired value; it was not added to Target.",
                    attributeName);
                return;
            }

            target[attributeName] = desiredValue;
        }
    }
}
