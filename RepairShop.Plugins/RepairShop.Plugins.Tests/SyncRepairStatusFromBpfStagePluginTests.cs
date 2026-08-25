using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Plugins.Incident;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class SyncRepairStatusFromBpfStagePluginTests
    {
        [Theory]
        [InlineData("Received", RepairStatusValues.Received)]
        [InlineData("Diagnosing", RepairStatusValues.Diagnosing)]
        [InlineData("Waiting For Approval", RepairStatusValues.WaitingForApproval)]
        [InlineData("Repair In Progress", RepairStatusValues.RepairInProgress)]
        [InlineData("Ready For Pickup", RepairStatusValues.ReadyForPickup)]
        public void KnownStage_UpdatesCaseToMappedRepairStatus(
            string stageName,
            int expectedRepairStatus)
        {
            BpfScenario scenario = CreateScenario(stageName, RepairStatusValues.Received - 1);
            Entity capturedUpdate = null;
            scenario.OrganizationService
                .Setup(service => service.Update(It.IsAny<Entity>()))
                .Callback<Entity>(entity => capturedUpdate = entity);

            Execute(scenario);

            Assert.NotNull(capturedUpdate);
            Assert.Equal(IncidentSchema.EntityLogicalName, capturedUpdate.LogicalName);
            Assert.Equal(scenario.CaseId, capturedUpdate.Id);
            Assert.Single(capturedUpdate.Attributes);
            Assert.Equal(
                expectedRepairStatus,
                capturedUpdate.GetAttributeValue<OptionSetValue>("cr1a3_repairstatus").Value);
        }

        [Fact]
        public void ActiveStageReferenceWithNullName_RetrievesProcessStageNameById()
        {
            BpfScenario scenario = CreateScenario("Diagnosing", RepairStatusValues.Received);
            EntityReference activeStage = scenario.PostImage.GetAttributeValue<EntityReference>(
                "activestageid");
            Assert.Null(activeStage.Name);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    "processstage",
                    scenario.StageId,
                    It.Is<ColumnSet>(columns =>
                        columns.Columns.Count == 1 && columns.Columns.Contains("stagename"))),
                Times.Once);
        }

        [Fact]
        public void PostImageActiveStage_IsUsedEvenWhenSparseTargetOmitsIt()
        {
            BpfScenario scenario = CreateScenario("Diagnosing", RepairStatusValues.Received);
            scenario.Target.Attributes.Remove(ComputerRepairProcessSchema.ActiveStage);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Update(It.Is<Entity>(entity =>
                    entity.Id == scenario.CaseId &&
                    entity.Attributes.Count == 1 &&
                    entity.GetAttributeValue<OptionSetValue>("cr1a3_repairstatus").Value ==
                        RepairStatusValues.Diagnosing)),
                Times.Once);
        }

        [Fact]
        public void AlreadySynchronizedCase_DoesNotUpdate()
        {
            BpfScenario scenario = CreateScenario("Diagnosing", RepairStatusValues.Diagnosing);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Fact]
        public void UnknownStage_DoesNotReadCaseAndDoesNotUpdate()
        {
            BpfScenario scenario = CreateScenario("Unmapped Stage", RepairStatusValues.Received);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    IncidentSchema.EntityLogicalName,
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Theory]
        [InlineData("Waiting For Parts")]
        [InlineData("Completed")]
        public void StagesAbsentFromLiveBpf_AreNotMapped(string stageName)
        {
            BpfScenario scenario = CreateScenario(stageName, RepairStatusValues.Received);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    IncidentSchema.EntityLogicalName,
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Fact]
        public void MissingRelatedCase_DoesNotReadCaseOrUpdate()
        {
            BpfScenario scenario = CreateScenario("Received", RepairStatusValues.Diagnosing);
            scenario.PostImage.Attributes.Remove(ComputerRepairProcessSchema.RelatedCase);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    IncidentSchema.EntityLogicalName,
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Fact]
        public void MissingActiveStageInPostImage_DoesNotCallDataverse()
        {
            BpfScenario scenario = CreateScenario("Diagnosing", RepairStatusValues.Received);
            scenario.PostImage.Attributes.Remove(ComputerRepairProcessSchema.ActiveStage);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        [Fact]
        public void MissingStageInformation_DoesNotUpdateCase()
        {
            BpfScenario scenario = CreateScenario(null, RepairStatusValues.Received);

            Execute(scenario);

            scenario.OrganizationService.Verify(
                service => service.Retrieve(
                    IncidentSchema.EntityLogicalName,
                    It.IsAny<Guid>(),
                    It.IsAny<ColumnSet>()),
                Times.Never);
            scenario.OrganizationService.Verify(
                service => service.Update(It.IsAny<Entity>()),
                Times.Never);
        }

        private static BpfScenario CreateScenario(string stageName, int currentRepairStatus)
        {
            Guid bpfId = Guid.NewGuid();
            Guid stageId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            var target = new Entity(ComputerRepairProcessSchema.EntityLogicalName, bpfId);
            target[ComputerRepairProcessSchema.ActiveStage] =
                new EntityReference(ComputerRepairProcessSchema.ProcessStageEntityLogicalName, stageId);

            var postImage = new Entity(ComputerRepairProcessSchema.EntityLogicalName, bpfId);
            postImage[ComputerRepairProcessSchema.ActiveStage] =
                new EntityReference(ComputerRepairProcessSchema.ProcessStageEntityLogicalName, stageId);
            postImage[ComputerRepairProcessSchema.RelatedCase] =
                new EntityReference(IncidentSchema.EntityLogicalName, caseId);

            var processStage = new Entity(
                ComputerRepairProcessSchema.ProcessStageEntityLogicalName,
                stageId);
            if (stageName != null)
            {
                processStage[ComputerRepairProcessSchema.ProcessStageName] = stageName;
            }

            var incident = new Entity(IncidentSchema.EntityLogicalName, caseId);
            incident[IncidentSchema.RepairStatus] = new OptionSetValue(currentRepairStatus);

            var organizationService = new Mock<IOrganizationService>();
            organizationService
                .Setup(service => service.Retrieve(
                    ComputerRepairProcessSchema.ProcessStageEntityLogicalName,
                    stageId,
                    It.IsAny<ColumnSet>()))
                .Returns(processStage);
            organizationService
                .Setup(service => service.Retrieve(
                    IncidentSchema.EntityLogicalName,
                    caseId,
                    It.IsAny<ColumnSet>()))
                .Returns(incident);

            var context = PluginTestContext.Create(
                ComputerRepairProcessSchema.EntityLogicalName,
                40,
                bpfId,
                target,
                postImage: postImage);

            return new BpfScenario
            {
                BpfId = bpfId,
                CaseId = caseId,
                StageId = stageId,
                Context = context,
                Target = target,
                PostImage = postImage,
                OrganizationService = organizationService
            };
        }

        private static void Execute(BpfScenario scenario)
        {
            var factory = new Mock<IOrganizationServiceFactory>();
            factory
                .Setup(item => item.CreateOrganizationService(scenario.Context.Object.UserId))
                .Returns(scenario.OrganizationService.Object);

            var provider = new TestServiceProvider()
                .Add<ITracingService>(new TestTracingService())
                .Add<IPluginExecutionContext>(scenario.Context.Object)
                .Add<IOrganizationServiceFactory>(factory.Object);

            new SyncRepairStatusFromBpfStagePlugin().Execute(provider);
        }

        private sealed class BpfScenario
        {
            internal Guid BpfId { get; set; }

            internal Guid CaseId { get; set; }

            internal Guid StageId { get; set; }

            internal Mock<IPluginExecutionContext> Context { get; set; }

            internal Entity Target { get; set; }

            internal Entity PostImage { get; set; }

            internal Mock<IOrganizationService> OrganizationService { get; set; }
        }
    }
}
