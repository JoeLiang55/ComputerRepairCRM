using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Plugins.SlaKpiInstance;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class EscalateRepairOnSlaStatusPluginTests
    {
        private const int InProgressStatus = 0;
        private const int SucceededStatus = 4;

        [Theory]
        [InlineData(SlaKpiInstanceStatusValues.NearingNoncompliance)]
        [InlineData(SlaKpiInstanceStatusValues.Noncompliant)]
        public void EscalationStatusTransition_EscalatesActiveCase(int newStatus)
        {
            SlaScenario scenario = CreateScenario(InProgressStatus, newStatus);
            Entity capturedUpdate = null;
            scenario.OrganizationService
                .Setup(service => service.Update(It.IsAny<Entity>()))
                .Callback<Entity>(entity => capturedUpdate = entity);

            Execute(scenario);

            Assert.NotNull(capturedUpdate);
            Assert.Equal(IncidentSchema.EntityLogicalName, capturedUpdate.LogicalName);
            Assert.Equal(scenario.Case.Id, capturedUpdate.Id);
            Assert.Single(capturedUpdate.Attributes);
            Assert.True(capturedUpdate.GetAttributeValue<bool>(IncidentSchema.IsEscalated));
        }

        [Fact]
        public void StatusUnchanged_DoesNothing()
        {
            SlaScenario scenario = CreateScenario(
                SlaKpiInstanceStatusValues.NearingNoncompliance,
                SlaKpiInstanceStatusValues.NearingNoncompliance);

            Execute(scenario);

            scenario.OrganizationService.VerifyNoOtherCalls();
        }

        [Fact]
        public void TransitionToUnrelatedStatus_DoesNothing()
        {
            SlaScenario scenario = CreateScenario(InProgressStatus, SucceededStatus);

            Execute(scenario);

            scenario.OrganizationService.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(RepairStatusValues.Completed)]
        [InlineData(RepairStatusValues.Cancelled)]
        public void TerminalCase_IsSkipped(int repairStatus)
        {
            SlaScenario scenario = CreateScenario(
                InProgressStatus,
                SlaKpiInstanceStatusValues.NearingNoncompliance,
                repairStatus);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Fact]
        public void AlreadyEscalatedCase_IsSkipped()
        {
            SlaScenario scenario = CreateScenario(
                InProgressStatus,
                SlaKpiInstanceStatusValues.Noncompliant,
                alreadyEscalated: true);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Fact]
        public void NoCaseReferencesKpiInstance_ExitsCleanly()
        {
            SlaScenario scenario = CreateScenario(
                InProgressStatus,
                SlaKpiInstanceStatusValues.NearingNoncompliance,
                includeCase: false);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Fact]
        public void StaleKpiInstanceThatIsNotCurrentOnCase_DoesNotEscalate()
        {
            Guid replacementKpiInstanceId = Guid.NewGuid();
            SlaScenario scenario = CreateScenario(
                InProgressStatus,
                SlaKpiInstanceStatusValues.Noncompliant,
                currentCaseKpiInstanceId: replacementKpiInstanceId);
            QueryExpression capturedQuery = null;
            scenario.OrganizationService
                .Setup(service => service.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Callback<QueryBase>(query => capturedQuery = query as QueryExpression)
                .Returns<QueryBase>(query => FindMatchingCase(query, scenario.Case));

            Execute(scenario);

            Assert.NotNull(capturedQuery);
            Assert.Equal(IncidentSchema.EntityLogicalName, capturedQuery.EntityName);
            Assert.Equal(1, capturedQuery.TopCount);
            Assert.Equal(2, capturedQuery.ColumnSet.Columns.Count);
            Assert.Contains(IncidentSchema.RepairStatus, capturedQuery.ColumnSet.Columns);
            Assert.Contains(IncidentSchema.IsEscalated, capturedQuery.ColumnSet.Columns);
            AssertQueryMatchesKpi(capturedQuery, scenario.SlaKpiInstanceId);
            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MissingRequiredImage_ThrowsRegistrationError(bool omitPreImage)
        {
            Guid slaKpiInstanceId = Guid.NewGuid();
            var target = new Entity(SlaKpiInstanceSchema.EntityLogicalName, slaKpiInstanceId);
            Entity preImage = omitPreImage
                ? null
                : CreateStatusImage(slaKpiInstanceId, InProgressStatus);
            Entity postImage = omitPreImage
                ? CreateStatusImage(
                    slaKpiInstanceId,
                    SlaKpiInstanceStatusValues.NearingNoncompliance)
                : null;
            var context = PluginTestContext.Create(
                SlaKpiInstanceSchema.EntityLogicalName,
                40,
                slaKpiInstanceId,
                target,
                preImage,
                postImage);
            var provider = new TestServiceProvider()
                .Add<ITracingService>(new TestTracingService())
                .Add<IPluginExecutionContext>(context.Object);

            Assert.Throws<InvalidPluginExecutionException>(
                () => new EscalateRepairOnSlaStatusPlugin().Execute(provider));
        }

        private static SlaScenario CreateScenario(
            int previousStatus,
            int currentStatus,
            int repairStatus = RepairStatusValues.RepairInProgress,
            bool alreadyEscalated = false,
            bool includeCase = true,
            Guid? currentCaseKpiInstanceId = null)
        {
            Guid slaKpiInstanceId = Guid.NewGuid();
            var target = new Entity(SlaKpiInstanceSchema.EntityLogicalName, slaKpiInstanceId);
            target[SlaKpiInstanceSchema.Status] = new OptionSetValue(currentStatus);
            Entity preImage = CreateStatusImage(slaKpiInstanceId, previousStatus);
            Entity postImage = CreateStatusImage(slaKpiInstanceId, currentStatus);

            Entity repairCase = null;
            if (includeCase)
            {
                repairCase = new Entity(IncidentSchema.EntityLogicalName, Guid.NewGuid());
                repairCase[IncidentSchema.SlaKpiInstance] = new EntityReference(
                    SlaKpiInstanceSchema.EntityLogicalName,
                    currentCaseKpiInstanceId ?? slaKpiInstanceId);
                repairCase[IncidentSchema.RepairStatus] = new OptionSetValue(repairStatus);
                repairCase[IncidentSchema.IsEscalated] = alreadyEscalated;
            }

            var organizationService = new Mock<IOrganizationService>(MockBehavior.Strict);
            organizationService
                .Setup(service => service.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns<QueryBase>(query => FindMatchingCase(query, repairCase));

            var context = PluginTestContext.Create(
                SlaKpiInstanceSchema.EntityLogicalName,
                40,
                slaKpiInstanceId,
                target,
                preImage,
                postImage);

            return new SlaScenario
            {
                SlaKpiInstanceId = slaKpiInstanceId,
                Case = repairCase,
                Context = context,
                OrganizationService = organizationService
            };
        }

        private static Entity CreateStatusImage(Guid slaKpiInstanceId, int status)
        {
            var image = new Entity(
                SlaKpiInstanceSchema.EntityLogicalName,
                slaKpiInstanceId);
            image[SlaKpiInstanceSchema.Status] = new OptionSetValue(status);
            return image;
        }

        private static EntityCollection FindMatchingCase(QueryBase query, Entity repairCase)
        {
            var results = new EntityCollection();
            var queryExpression = query as QueryExpression;
            if (queryExpression == null || repairCase == null)
            {
                return results;
            }

            EntityReference currentKpi = repairCase.GetAttributeValue<EntityReference>(
                IncidentSchema.SlaKpiInstance);
            if (currentKpi != null && QueryMatchesKpi(queryExpression, currentKpi.Id))
            {
                results.Entities.Add(repairCase);
            }

            return results;
        }

        private static bool QueryMatchesKpi(QueryExpression query, Guid expectedKpiId)
        {
            foreach (ConditionExpression condition in query.Criteria.Conditions)
            {
                if (string.Equals(
                        condition.AttributeName,
                        IncidentSchema.SlaKpiInstance,
                        StringComparison.OrdinalIgnoreCase) &&
                    condition.Operator == ConditionOperator.Equal &&
                    condition.Values.Count == 1 &&
                    condition.Values[0] is Guid &&
                    (Guid)condition.Values[0] == expectedKpiId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertQueryMatchesKpi(QueryExpression query, Guid expectedKpiId)
        {
            Assert.True(
                QueryMatchesKpi(query, expectedKpiId),
                "The Case query must filter gsic_14dayreminderkpi by the triggering SLA KPI instance ID.");
        }

        private static void Execute(SlaScenario scenario)
        {
            var factory = new Mock<IOrganizationServiceFactory>(MockBehavior.Strict);
            factory
                .Setup(item => item.CreateOrganizationService(scenario.Context.Object.UserId))
                .Returns(scenario.OrganizationService.Object);
            var provider = new TestServiceProvider()
                .Add<ITracingService>(new TestTracingService())
                .Add<IPluginExecutionContext>(scenario.Context.Object)
                .Add<IOrganizationServiceFactory>(factory.Object);

            new EscalateRepairOnSlaStatusPlugin().Execute(provider);
        }

        private sealed class SlaScenario
        {
            internal Guid SlaKpiInstanceId { get; set; }

            internal Entity Case { get; set; }

            internal Mock<IPluginExecutionContext> Context { get; set; }

            internal Mock<IOrganizationService> OrganizationService { get; set; }
        }
    }
}
