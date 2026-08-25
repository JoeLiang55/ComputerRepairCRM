using System;
using Microsoft.Xrm.Sdk;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Services;

namespace RepairShop.Plugins.Plugins.Incident
{
    /// <summary>
    /// Maintains Repair Due Date from Date Received and standard Case Priority.
    /// Register synchronous PreOperation steps for Create and Update/incident.
    /// </summary>
    public sealed class CalculateRepairDueDatePlugin : PluginBase
    {
        public const string PreImageAlias = "PreImage";

        private const int PreOperationStage = 20;
        private const string CreateMessage = "Create";
        private const string UpdateMessage = "Update";

        private readonly string dueDateLogicalName;
        private readonly RepairSlaCalculator calculator;

        public CalculateRepairDueDatePlugin()
            : this(IncidentSchema.RepairDueDate, new RepairSlaCalculator())
        {
        }

        internal CalculateRepairDueDatePlugin(
            string dueDateLogicalName,
            RepairSlaCalculator calculator)
        {
            this.dueDateLogicalName = dueDateLogicalName;
            this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        protected override string UnexpectedErrorMessage =>
            "The repair due date calculation failed.";

        protected override void ExecutePlugin(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            bool isCreate = string.Equals(
                context.MessageName,
                CreateMessage,
                StringComparison.OrdinalIgnoreCase);
            bool isUpdate = string.Equals(
                context.MessageName,
                UpdateMessage,
                StringComparison.OrdinalIgnoreCase);

            if ((!isCreate && !isUpdate) ||
                !string.Equals(
                    context.PrimaryEntityName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase) ||
                context.Stage != PreOperationStage)
            {
                tracingService.Trace(
                    "Execution skipped: expected Create or Update on incident at PreOperation; received {0}/{1}/stage {2}.",
                    context.MessageName,
                    context.PrimaryEntityName,
                    context.Stage);
                return;
            }

            EnsureDueDateSchemaIsResolved();
            tracingService.Trace("Repair SLA operation: {0}.", isCreate ? CreateMessage : UpdateMessage);

            Entity target;
            if (!TryGetTarget(context, out target) ||
                !string.Equals(
                    target.LogicalName,
                    IncidentSchema.EntityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("Calculation skipped: a valid incident Target was not supplied.");
                return;
            }

            if (isCreate)
            {
                ApplyForCreate(target, tracingService);
                return;
            }

            ApplyForUpdate(target, context, tracingService);
        }

        private void ApplyForCreate(Entity target, ITracingService tracingService)
        {
            DateTime? dateReceived = GetDateTime(target, IncidentSchema.DateReceived);
            int? priorityValue = GetChoiceValue(target, IncidentSchema.Priority);
            TraceEffectiveValues(tracingService, dateReceived, priorityValue);

            if (!dateReceived.HasValue)
            {
                tracingService.Trace(
                    "Calculation skipped: Date Received is missing on Create; no fallback time was used.");
                return;
            }

            RepairPriority priority;
            if (!TryMapPriority(priorityValue, out priority))
            {
                tracingService.Trace(
                    "Calculation skipped: Priority is missing or unrecognized; Repair Due Date was not generated.");
                return;
            }

            SetCalculatedDueDate(target, dateReceived.Value, priority, tracingService);
        }

        private void ApplyForUpdate(
            Entity target,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            bool dateSubmitted = target.Contains(IncidentSchema.DateReceived);
            bool prioritySubmitted = target.Contains(IncidentSchema.Priority);
            if (!dateSubmitted && !prioritySubmitted)
            {
                tracingService.Trace(
                    "Calculation skipped: neither Priority nor Date Received was present in Target.");
                return;
            }

            Entity preImage;
            if (context.PreEntityImages == null ||
                !context.PreEntityImages.TryGetValue(PreImageAlias, out preImage) ||
                preImage == null)
            {
                tracingService.Trace(
                    "Calculation skipped safely: required Pre Image '{0}' is missing.",
                    PreImageAlias);
                return;
            }

            DateTime? oldDateReceived = GetDateTime(preImage, IncidentSchema.DateReceived);
            int? oldPriorityValue = GetChoiceValue(preImage, IncidentSchema.Priority);
            DateTime? effectiveDateReceived = dateSubmitted
                ? GetDateTime(target, IncidentSchema.DateReceived)
                : oldDateReceived;
            int? effectivePriorityValue = prioritySubmitted
                ? GetChoiceValue(target, IncidentSchema.Priority)
                : oldPriorityValue;

            TraceEffectiveValues(tracingService, effectiveDateReceived, effectivePriorityValue);

            bool dateChanged = dateSubmitted && oldDateReceived != effectiveDateReceived;
            bool priorityChanged = prioritySubmitted && oldPriorityValue != effectivePriorityValue;
            if (!dateChanged && !priorityChanged)
            {
                tracingService.Trace(
                    "Calculation skipped: submitted SLA inputs have the same effective values.");
                return;
            }

            if (dateSubmitted && !effectiveDateReceived.HasValue)
            {
                target[dueDateLogicalName] = null;
                tracingService.Trace(
                    "Repair Due Date cleared because Date Received was explicitly cleared.");
                return;
            }

            RepairPriority priority;
            if (!effectiveDateReceived.HasValue)
            {
                tracingService.Trace(
                    "Calculation skipped: effective Date Received is missing; no fallback time was used.");
                return;
            }

            if (!TryMapPriority(effectivePriorityValue, out priority))
            {
                target[dueDateLogicalName] = null;
                tracingService.Trace(
                    "Repair Due Date cleared because effective Priority is missing or unrecognized.");
                return;
            }

            SetCalculatedDueDate(target, effectiveDateReceived.Value, priority, tracingService);
        }

        private void SetCalculatedDueDate(
            Entity target,
            DateTime dateReceived,
            RepairPriority priority,
            ITracingService tracingService)
        {
            int businessDays = calculator.GetBusinessDays(priority);
            DateTime dueDate = calculator.CalculateDueDate(dateReceived, priority);
            tracingService.Trace("Selected SLA: {0} business day(s).", businessDays);
            tracingService.Trace("Calculated Repair Due Date: {0:o}.", dueDate);
            target[dueDateLogicalName] = dueDate;
        }

        private void EnsureDueDateSchemaIsResolved()
        {
            if (string.IsNullOrWhiteSpace(dueDateLogicalName) ||
                string.Equals(
                    dueDateLogicalName,
                    IncidentSchema.UnresolvedRepairDueDate,
                    StringComparison.Ordinal))
            {
                throw new InvalidPluginExecutionException(
                    "Repair Due Date logical name is unresolved. Create the column in Power Apps and replace IncidentSchema.RepairDueDate with its generated logical name before deployment.");
            }
        }

        private static void TraceEffectiveValues(
            ITracingService tracingService,
            DateTime? dateReceived,
            int? priorityValue)
        {
            tracingService.Trace(
                "Effective Date Received: {0}.",
                dateReceived.HasValue ? dateReceived.Value.ToString("o") : "<missing>");
            tracingService.Trace(
                "Effective Priority: {0}.",
                priorityValue.HasValue ? priorityValue.Value.ToString() : "<missing>");
        }

        private static DateTime? GetDateTime(Entity entity, string attributeName)
        {
            if (entity == null || !entity.Contains(attributeName) || entity[attributeName] == null)
            {
                return null;
            }

            return entity.GetAttributeValue<DateTime>(attributeName);
        }

        private static int? GetChoiceValue(Entity entity, string attributeName)
        {
            OptionSetValue value = entity != null
                ? entity.GetAttributeValue<OptionSetValue>(attributeName)
                : null;
            return value == null ? (int?)null : value.Value;
        }

        private static bool TryMapPriority(int? value, out RepairPriority priority)
        {
            switch (value)
            {
                case IncidentPriorityValues.Low:
                    priority = RepairPriority.Low;
                    return true;
                case IncidentPriorityValues.Normal:
                    priority = RepairPriority.Normal;
                    return true;
                case IncidentPriorityValues.High:
                    priority = RepairPriority.High;
                    return true;
                default:
                    priority = default(RepairPriority);
                    return false;
            }
        }
    }
}
