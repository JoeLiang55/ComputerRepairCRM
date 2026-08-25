using System;
using Microsoft.Xrm.Sdk;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Plugins.Incident;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class CompleteRepairOnStatusChangePluginTests
    {
        [Fact]
        public void RepairStatus_UsesConfirmedLogicalName()
        {
            Assert.Equal("cr1a3_repairstatus", IncidentSchema.RepairStatus);
            Assert.Equal("cr1a3_datereceived", IncidentSchema.DateReceived);
            Assert.Equal("cr1a3_estimatedcost", IncidentSchema.EstimatedCost);
            Assert.Equal("cr1a3_finalcost", IncidentSchema.FinalCost);
            Assert.Equal("gsic_completiondate", IncidentSchema.CompletionDate);
            Assert.Equal("gsic_repairduration", IncidentSchema.RepairDuration);
        }

        [Fact]
        public void RepairStatus_UsesConfirmedChoiceValues()
        {
            Assert.Equal(702670004, RepairStatusValues.WaitingForParts);
            Assert.Equal(702670006, RepairStatusValues.Completed);
            Assert.Equal(702670007, RepairStatusValues.Cancelled);
        }

        [Fact]
        public void ConfirmedLiteralPreImageAttributes_AreConsumed()
        {
            DateTime beforeExecution = DateTime.UtcNow;
            DateTime receivedUtc = beforeExecution.AddMinutes(-30).AddSeconds(-5);
            var target = new Entity("incident", Guid.NewGuid());
            target["cr1a3_repairstatus"] = new OptionSetValue(702670006);

            var preImage = new Entity("incident", target.Id);
            preImage["cr1a3_datereceived"] = receivedUtc;
            preImage["cr1a3_estimatedcost"] = new Money(89.95m);
            preImage["cr1a3_finalcost"] = null;
            preImage["cr1a3_repairstatus"] = new OptionSetValue(702670000);

            Execute(target, preImage);

            Assert.Equal(89.95m, target.GetAttributeValue<Money>("cr1a3_finalcost").Value);
            Assert.InRange(
                target.GetAttributeValue<DateTime>("gsic_completiondate"),
                beforeExecution,
                DateTime.UtcNow);
            Assert.Equal(30, target.GetAttributeValue<int>("gsic_repairduration"));
        }

        [Fact]
        public void CompletionTransition_SetsDateDurationAndCopiesEstimatedCost()
        {
            DateTime beforeExecution = DateTime.UtcNow;
            DateTime receivedUtc = beforeExecution.AddMinutes(-90).AddSeconds(-5);
            Entity target = CreateTarget(RepairStatusValues.Completed);
            Entity preImage = CreatePreImage(receivedUtc);
            preImage[IncidentSchema.EstimatedCost] = new Money(125.50m);

            Execute(target, preImage);

            DateTime completionDate = target.GetAttributeValue<DateTime>(IncidentSchema.CompletionDate);
            Assert.InRange(completionDate, beforeExecution, DateTime.UtcNow);
            Assert.Equal(90, target.GetAttributeValue<int>(IncidentSchema.RepairDuration));
            Assert.Equal(125.50m, target.GetAttributeValue<Money>(IncidentSchema.FinalCost).Value);
        }

        [Fact]
        public void NonCompletedStatus_DoesNothing()
        {
            Entity target = CreateTarget(RepairStatusValues.Diagnosing);

            Execute(target, null);

            Assert.False(target.Contains(IncidentSchema.CompletionDate));
            Assert.False(target.Contains(IncidentSchema.RepairDuration));
            Assert.False(target.Contains(IncidentSchema.FinalCost));
        }

        [Fact]
        public void AlreadyCompletedStatus_DoesNothing()
        {
            Entity target = CreateTarget(RepairStatusValues.Completed);
            Entity preImage = CreatePreImage(DateTime.UtcNow.AddMinutes(-10));
            preImage[IncidentSchema.RepairStatus] =
                new OptionSetValue(RepairStatusValues.Completed);

            Execute(target, preImage);

            Assert.False(target.Contains(IncidentSchema.CompletionDate));
            Assert.False(target.Contains(IncidentSchema.RepairDuration));
            Assert.False(target.Contains(IncidentSchema.FinalCost));
        }

        [Fact]
        public void MissingDateReceived_RejectsCompletion()
        {
            Entity target = CreateTarget(RepairStatusValues.Completed);
            Entity preImage = CreatePreImage(null);

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(target, preImage));

            Assert.Contains("Date Received is missing", exception.Message);
        }

        [Fact]
        public void MissingPreImage_RejectsCompletion()
        {
            Entity target = CreateTarget(RepairStatusValues.Completed);

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(target, null));

            Assert.Contains("Pre Image", exception.Message);
        }

        [Fact]
        public void FutureDateReceived_RejectsCompletion()
        {
            Entity target = CreateTarget(RepairStatusValues.Completed);
            Entity preImage = CreatePreImage(DateTime.UtcNow.AddHours(1));

            InvalidPluginExecutionException exception = Assert.Throws<InvalidPluginExecutionException>(
                () => Execute(target, preImage));

            Assert.Contains("Date Received is in the future", exception.Message);
        }

        [Fact]
        public void Duration_TruncatesPartialMinutes()
        {
            Entity target = CreateTarget(RepairStatusValues.Completed);
            Entity preImage = CreatePreImage(DateTime.UtcNow.AddMinutes(-12).AddSeconds(-5));

            Execute(target, preImage);

            Assert.Equal(12, target.GetAttributeValue<int>(IncidentSchema.RepairDuration));
        }

        [Fact]
        public void ExistingFinalCost_IsPreserved()
        {
            Entity target = CreateTarget(RepairStatusValues.Completed);
            Entity preImage = CreatePreImage(DateTime.UtcNow.AddMinutes(-5));
            preImage[IncidentSchema.EstimatedCost] = new Money(125m);
            preImage[IncidentSchema.FinalCost] = new Money(175m);

            Execute(target, preImage);

            Assert.False(target.Contains(IncidentSchema.FinalCost));
            Assert.Equal(175m, preImage.GetAttributeValue<Money>(IncidentSchema.FinalCost).Value);
        }

        [Fact]
        public void UnrelatedUpdate_DoesNothing()
        {
            var target = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
            target["title"] = "Updated title";

            Execute(target, null);

            Assert.False(target.Contains(IncidentSchema.CompletionDate));
            Assert.False(target.Contains(IncidentSchema.RepairDuration));
            Assert.False(target.Contains(IncidentSchema.FinalCost));
        }

        [Fact]
        public void CancelledStatus_DoesNothing()
        {
            Entity target = CreateTarget(RepairStatusValues.Cancelled);

            Execute(target, null);

            Assert.False(target.Contains(IncidentSchema.CompletionDate));
            Assert.False(target.Contains(IncidentSchema.RepairDuration));
            Assert.False(target.Contains(IncidentSchema.FinalCost));
        }

        private static Entity CreateTarget(int repairStatus)
        {
            var target = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
            target[IncidentSchema.RepairStatus] = new OptionSetValue(repairStatus);
            return target;
        }

        private static Entity CreatePreImage(DateTime? dateReceived)
        {
            var preImage = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
            preImage[IncidentSchema.RepairStatus] = new OptionSetValue(RepairStatusValues.Received);
            if (dateReceived.HasValue)
            {
                preImage[IncidentSchema.DateReceived] = dateReceived.Value;
            }

            return preImage;
        }

        private static void Execute(Entity target, Entity preImage)
        {
            var context = PluginTestContext.Create(
                IncidentSchema.EntityLogicalName,
                20,
                target.Id,
                target,
                preImage);
            var provider = new TestServiceProvider()
                .Add<ITracingService>(new TestTracingService())
                .Add<IPluginExecutionContext>(context.Object);

            var plugin = new CompleteRepairOnStatusChangePlugin();
            plugin.Execute(provider);
        }
    }
}
