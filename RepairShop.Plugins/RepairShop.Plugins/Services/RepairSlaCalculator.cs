using System;

namespace RepairShop.Plugins.Services
{
    public enum RepairPriority
    {
        Low,
        Normal,
        High
    }

    /// <summary>
    /// Contains deterministic repair SLA rules without Dataverse dependencies.
    /// Weekend detection is isolated so a holiday/business-closure calendar can
    /// replace it later without changing plug-in plumbing.
    /// </summary>
    public sealed class RepairSlaCalculator
    {
        public int GetBusinessDays(RepairPriority priority)
        {
            switch (priority)
            {
                case RepairPriority.Low:
                    return 10;
                case RepairPriority.Normal:
                    return 5;
                case RepairPriority.High:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(priority));
            }
        }

        public DateTime CalculateDueDate(DateTime start, RepairPriority priority)
        {
            return AddBusinessDays(start, GetBusinessDays(priority));
        }

        public DateTime AddBusinessDays(DateTime start, int businessDays)
        {
            if (businessDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(businessDays));
            }

            DateTime result = start;
            int daysAdded = 0;
            while (daysAdded < businessDays)
            {
                result = result.AddDays(1);
                if (IsBusinessDay(result))
                {
                    daysAdded++;
                }
            }

            return result;
        }

        private static bool IsBusinessDay(DateTime value)
        {
            return value.DayOfWeek != DayOfWeek.Saturday &&
                   value.DayOfWeek != DayOfWeek.Sunday;
        }
    }
}
