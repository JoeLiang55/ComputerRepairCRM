using System;
using Microsoft.Xrm.Sdk;
using Moq;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Plugins.Incident;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class PreventInvalidRepairStatusTransitionPluginTests
    {
        [Fact]
        public void ValidTransition_CompletesWithoutError()
        {
            Execute(CreateTarget(RepairStatusValues.Diagnosing), CreatePreImage(RepairStatusValues.Received));
        }

        [Fact]
        public void SameValueUpdate_CompletesWithoutError()
        {
            Execute(CreateTarget(RepairStatusValues.Diagnosing), CreatePreImage(RepairStatusValues.Diagnosing));
        }

        [Fact]
        public void InvalidTransition_ThrowsUserFacingStatusNames()
        {
            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(
                    CreateTarget(RepairStatusValues.RepairInProgress),
                    CreatePreImage(RepairStatusValues.Received)));

            Assert.Equal(
                "Invalid Repair Status transition: Received cannot be changed directly to Repair in Progress.",
                exception.Message);
            Assert.DoesNotContain(RepairStatusValues.Received.ToString(), exception.Message);
            Assert.DoesNotContain(RepairStatusValues.RepairInProgress.ToString(), exception.Message);
        }

        [Fact]
        public void TargetMissingRepairStatus_DoesNothing()
        {
            var target = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
            target[IncidentSchema.EstimatedCost] = new Money(50m);

            Execute(target, null);
        }

        [Fact]
        public void MissingPreImage_ThrowsConfigurationError()
        {
            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(CreateTarget(RepairStatusValues.Diagnosing), null));

            Assert.Contains("Pre Image", exception.Message);
            Assert.Contains("PreImage", exception.Message);
        }

        [Fact]
        public void PreImageMissingRepairStatus_ThrowsConfigurationError()
        {
            var preImage = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(CreateTarget(RepairStatusValues.Diagnosing), preImage));

            Assert.Contains(IncidentSchema.RepairStatus, exception.Message);
        }

        [Theory]
        [InlineData(999999999, RepairStatusValues.Received)]
        [InlineData(RepairStatusValues.Received, 999999999)]
        public void UnknownChoiceValue_ThrowsWithoutExposingTheValue(
            int previousStatus,
            int requestedStatus)
        {
            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(CreateTarget(requestedStatus), CreatePreImage(previousStatus)));

            Assert.Contains("unsupported Repair Status", exception.Message);
            Assert.DoesNotContain("999999999", exception.Message);
        }

        [Fact]
        public void UnrelatedUpdateContext_DoesNothing()
        {
            Entity target = CreateTarget(RepairStatusValues.ReadyForPickup);
            Entity preImage = CreatePreImage(RepairStatusValues.Received);
            Mock<IPluginExecutionContext> context = CreateContext(target, preImage);
            context.SetupGet(item => item.MessageName).Returns("Create");

            Execute(context);
        }

        private static Entity CreateTarget(int repairStatus)
        {
            var target = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
            target[IncidentSchema.RepairStatus] = new OptionSetValue(repairStatus);
            return target;
        }

        private static Entity CreatePreImage(int repairStatus)
        {
            var preImage = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
            preImage[IncidentSchema.RepairStatus] = new OptionSetValue(repairStatus);
            return preImage;
        }

        private static void Execute(Entity target, Entity preImage)
        {
            Execute(CreateContext(target, preImage));
        }

        private static Mock<IPluginExecutionContext> CreateContext(Entity target, Entity preImage)
        {
            return PluginTestContext.Create(
                IncidentSchema.EntityLogicalName,
                10,
                target.Id,
                target,
                preImage);
        }

        private static void Execute(Mock<IPluginExecutionContext> context)
        {
            var provider = new TestServiceProvider()
                .Add<ITracingService>(new TestTracingService())
                .Add<IPluginExecutionContext>(context.Object);

            new PreventInvalidRepairStatusTransitionPlugin().Execute(provider);
        }
    }
}
