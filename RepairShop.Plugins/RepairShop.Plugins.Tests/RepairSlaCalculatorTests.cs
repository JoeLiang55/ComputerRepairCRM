using System;
using RepairShop.Plugins.Services;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class RepairSlaCalculatorTests
    {
        private readonly RepairSlaCalculator calculator = new RepairSlaCalculator();

        [Fact]
        public void MondayPlusFiveBusinessDays_IsNextMonday()
        {
            Assert.Equal(
                new DateTime(2026, 8, 31),
                calculator.AddBusinessDays(new DateTime(2026, 8, 24), 5));
        }

        [Fact]
        public void FridayPlusOneBusinessDay_IsMonday()
        {
            Assert.Equal(
                new DateTime(2026, 8, 24),
                calculator.AddBusinessDays(new DateTime(2026, 8, 21), 1));
        }

        [Fact]
        public void FridayPlusTwoBusinessDays_IsTuesday()
        {
            Assert.Equal(
                new DateTime(2026, 8, 25),
                calculator.AddBusinessDays(new DateTime(2026, 8, 21), 2));
        }

        [Fact]
        public void SaturdayStart_CountsMondayAsFirstBusinessDay()
        {
            Assert.Equal(
                new DateTime(2026, 8, 24),
                calculator.AddBusinessDays(new DateTime(2026, 8, 22), 1));
        }

        [Fact]
        public void SundayStart_CountsMondayAsFirstBusinessDay()
        {
            Assert.Equal(
                new DateTime(2026, 8, 24),
                calculator.AddBusinessDays(new DateTime(2026, 8, 23), 1));
        }

        [Fact]
        public void CrossingMultipleWeekends_SkipsEachWeekend()
        {
            Assert.Equal(
                new DateTime(2026, 9, 1),
                calculator.AddBusinessDays(new DateTime(2026, 8, 21), 7));
        }

        [Fact]
        public void Calculation_PreservesTimeOfDayAndKind()
        {
            var start = new DateTime(2026, 8, 21, 14, 37, 12, DateTimeKind.Utc);

            DateTime result = calculator.AddBusinessDays(start, 2);

            Assert.Equal(new DateTime(2026, 8, 25, 14, 37, 12, DateTimeKind.Utc), result);
            Assert.Equal(DateTimeKind.Utc, result.Kind);
        }

        [Theory]
        [InlineData(RepairPriority.Low, 10)]
        [InlineData(RepairPriority.Normal, 5)]
        [InlineData(RepairPriority.High, 2)]
        public void PriorityRule_ReturnsConfiguredBusinessDays(
            RepairPriority priority,
            int expectedDays)
        {
            Assert.Equal(expectedDays, calculator.GetBusinessDays(priority));
        }
    }
}
