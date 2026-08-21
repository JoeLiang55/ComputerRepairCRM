using System;
using Microsoft.Xrm.Sdk;
using RepairShop.Plugins.Model;

namespace RepairShop.Plugins.Services
{
    internal static class RepairCompletionService
    {
        internal static bool IsCompletedStatus(Entity target, int completedStatusValue)
        {
            OptionSetValue status =
                target.GetAttributeValue<OptionSetValue>(IncidentSchema.RepairStatus);
            return status != null && status.Value == completedStatusValue;
        }

        internal static bool IsTransitionToCompleted(
            Entity target,
            Entity preImage,
            int completedStatusValue)
        {
            if (!IsCompletedStatus(target, completedStatusValue))
            {
                return false;
            }

            OptionSetValue previousStatus =
                preImage.GetAttributeValue<OptionSetValue>(IncidentSchema.RepairStatus);
            return previousStatus == null || previousStatus.Value != completedStatusValue;
        }

        internal static int ApplyCompletion(
            Entity target,
            Entity preImage,
            DateTime completionDateUtc)
        {
            DateTime? received = GetEffectiveNullableValue<DateTime>(
                target,
                preImage,
                IncidentSchema.DateReceived);
            if (!received.HasValue)
            {
                throw new InvalidPluginExecutionException(
                    "The Case cannot be completed because Date Received is missing.");
            }

            int durationMinutes = CalculateDurationMinutes(received.Value, completionDateUtc);

            target[IncidentSchema.CompletionDate] = completionDateUtc;
            target[IncidentSchema.RepairDuration] = durationMinutes;

            Money finalCost = GetEffectiveValue<Money>(target, preImage, IncidentSchema.FinalCost);
            if (finalCost == null)
            {
                Money estimatedCost = GetEffectiveValue<Money>(
                    target,
                    preImage,
                    IncidentSchema.EstimatedCost);
                if (estimatedCost != null)
                {
                    target[IncidentSchema.FinalCost] = new Money(estimatedCost.Value);
                }
            }

            return durationMinutes;
        }

        internal static int CalculateDurationMinutes(
            DateTime dateReceived,
            DateTime completionDateUtc)
        {
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
    }
}
