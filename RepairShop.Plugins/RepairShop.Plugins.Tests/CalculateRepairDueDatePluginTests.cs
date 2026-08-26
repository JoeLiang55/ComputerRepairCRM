using System;
using Microsoft.Xrm.Sdk;
using Moq;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Plugins.Incident;
using RepairShop.Plugins.Services;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class CalculateRepairDueDatePluginTests
    {
        private const string TestDueDate = "test_repairduedate";

        [Fact]
        public void StandardPrioritySchema_UsesSdkConventionValues()
        {
            Assert.Equal("prioritycode", IncidentSchema.Priority);
            Assert.Equal(1, IncidentPriorityValues.High);
            Assert.Equal(2, IncidentPriorityValues.Normal);
            Assert.Equal(3, IncidentPriorityValues.Low);
            Assert.Equal("gsic_repairduedate", IncidentSchema.RepairDueDate);
        }

        [Fact]
        public void CreateWithDateReceivedAndPriority_SetsDueDate()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = new DateTime(2026, 8, 24, 9, 30, 0);
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.Normal);

            Execute(target, "Create");

            Assert.Equal(
                new DateTime(2026, 8, 31, 9, 30, 0),
                target.GetAttributeValue<DateTime>(TestDueDate));
            Assert.Equal(3, target.Attributes.Count);
        }

        [Fact]
        public void CreateWithoutDateReceived_DoesNotFabricateDueDate()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.High);

            Execute(target, "Create");

            Assert.False(target.Contains(TestDueDate));
        }

        [Fact]
        public void UpdatePriority_UsesPreImageDateReceived()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.High);
            Entity preImage = CreatePreImage(
                new DateTime(2026, 8, 21, 10, 0, 0),
                IncidentPriorityValues.Normal);

            Execute(target, "Update", preImage);

            Assert.Equal(
                new DateTime(2026, 8, 25, 10, 0, 0),
                target.GetAttributeValue<DateTime>(TestDueDate));
            Assert.Equal(2, target.Attributes.Count);
        }

        [Fact]
        public void UpdateDateReceived_UsesPreImagePriority()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = new DateTime(2026, 8, 25, 11, 15, 0);
            Entity preImage = CreatePreImage(
                new DateTime(2026, 8, 24, 11, 15, 0),
                IncidentPriorityValues.Low);

            Execute(target, "Update", preImage);

            Assert.Equal(
                new DateTime(2026, 9, 8, 11, 15, 0),
                target.GetAttributeValue<DateTime>(TestDueDate));
            Assert.Equal(2, target.Attributes.Count);
        }

        [Fact]
        public void UpdateBoth_UsesBothNewValues()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = new DateTime(2026, 8, 21, 8, 0, 0);
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.High);
            Entity preImage = CreatePreImage(
                new DateTime(2026, 8, 20, 8, 0, 0),
                IncidentPriorityValues.Low);

            Execute(target, "Update", preImage);

            Assert.Equal(
                new DateTime(2026, 8, 25, 8, 0, 0),
                target.GetAttributeValue<DateTime>(TestDueDate));
            Assert.Equal(3, target.Attributes.Count);
        }

        [Fact]
        public void UnrelatedUpdate_DoesNothing()
        {
            Entity target = CreateTarget();
            target["title"] = "Changed";

            Execute(target, "Update");

            Assert.Single(target.Attributes);
            Assert.False(target.Contains(TestDueDate));
        }

        [Fact]
        public void SameEffectiveValues_DoNothing()
        {
            DateTime received = new DateTime(2026, 8, 24, 12, 0, 0);
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = received;
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.Normal);
            Entity preImage = CreatePreImage(received, IncidentPriorityValues.Normal);

            Execute(target, "Update", preImage);

            Assert.False(target.Contains(TestDueDate));
            Assert.Equal(2, target.Attributes.Count);
        }

        [Fact]
        public void ClearingDateReceived_ClearsDueDate()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = null;
            Entity preImage = CreatePreImage(
                new DateTime(2026, 8, 24, 12, 0, 0),
                IncidentPriorityValues.Normal);

            Execute(target, "Update", preImage);

            Assert.True(target.Contains(TestDueDate));
            Assert.Null(target[TestDueDate]);
            Assert.Equal(2, target.Attributes.Count);
        }

        [Fact]
        public void UnknownPriority_ClearsDueDateSafely()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.Priority] = new OptionSetValue(99);
            Entity preImage = CreatePreImage(
                new DateTime(2026, 8, 24, 12, 0, 0),
                IncidentPriorityValues.Normal);

            Execute(target, "Update", preImage);

            Assert.True(target.Contains(TestDueDate));
            Assert.Null(target[TestDueDate]);
        }

        [Fact]
        public void UnknownPriorityOnCreate_DoesNotGenerateDueDate()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = new DateTime(2026, 8, 24);
            target[IncidentSchema.Priority] = new OptionSetValue(99);

            Execute(target, "Create");

            Assert.False(target.Contains(TestDueDate));
        }

        [Fact]
        public void MissingRequiredPreImage_IsHandledWithoutMutation()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.High);

            Execute(target, "Update");

            Assert.False(target.Contains(TestDueDate));
            Assert.Single(target.Attributes);
        }

        [Fact]
        public void DefaultPlugin_UsesResolvedDueDateSchema()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = new DateTime(2026, 8, 24);
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.Normal);

            Execute(target, "Create", null, new CalculateRepairDueDatePlugin());

            Assert.Equal(
                new DateTime(2026, 8, 31),
                target.GetAttributeValue<DateTime>(IncidentSchema.RepairDueDate));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(IncidentSchema.UnresolvedRepairDueDate)]
        public void Plugin_RefusesMissingOrUnresolvedDueDateSchema(string dueDateLogicalName)
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = new DateTime(2026, 8, 24);
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.Normal);
            var plugin = new CalculateRepairDueDatePlugin(
                dueDateLogicalName,
                new RepairSlaCalculator());

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(target, "Create", null, plugin));

            Assert.Contains("logical name is unresolved", exception.Message);
        }

        [Fact]
        public void Plugin_DoesNotRequestOrganizationServiceOrIssueUpdate()
        {
            Entity target = CreateTarget();
            target[IncidentSchema.DateReceived] = new DateTime(2026, 8, 24);
            target[IncidentSchema.Priority] = new OptionSetValue(IncidentPriorityValues.Normal);
            var factory = new Mock<IOrganizationServiceFactory>(MockBehavior.Strict);

            Execute(target, "Create", null, null, factory.Object);

            factory.VerifyNoOtherCalls();
        }

        private static Entity CreateTarget()
        {
            return new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
        }

        private static Entity CreatePreImage(DateTime dateReceived, int priority)
        {
            var image = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
            image[IncidentSchema.DateReceived] = dateReceived;
            image[IncidentSchema.Priority] = new OptionSetValue(priority);
            return image;
        }

        private static void Execute(
            Entity target,
            string messageName,
            Entity preImage = null,
            CalculateRepairDueDatePlugin plugin = null,
            IOrganizationServiceFactory serviceFactory = null)
        {
            var context = PluginTestContext.Create(
                IncidentSchema.EntityLogicalName,
                20,
                target.Id,
                target,
                preImage,
                messageName: messageName);
            var provider = new TestServiceProvider()
                .Add<ITracingService>(new TestTracingService())
                .Add<IPluginExecutionContext>(context.Object);
            if (serviceFactory != null)
            {
                provider.Add<IOrganizationServiceFactory>(serviceFactory);
            }

            (plugin ?? new CalculateRepairDueDatePlugin(
                TestDueDate,
                new RepairSlaCalculator())).Execute(provider);
        }
    }
}
