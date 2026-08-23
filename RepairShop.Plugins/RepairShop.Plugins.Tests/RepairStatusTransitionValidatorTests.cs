using System.Collections.Generic;
using RepairShop.Plugins.Model;
using RepairShop.Plugins.Services;
using Xunit;

namespace RepairShop.Plugins.Tests
{
    public sealed class RepairStatusTransitionValidatorTests
    {
        public static IEnumerable<object[]> ValidTransitions
        {
            get
            {
                yield return Transition(RepairStatusValues.Received, RepairStatusValues.Diagnosing);
                yield return Transition(RepairStatusValues.Diagnosing, RepairStatusValues.WaitingForApproval);
                yield return Transition(RepairStatusValues.WaitingForApproval, RepairStatusValues.RepairInProgress);
                yield return Transition(RepairStatusValues.RepairInProgress, RepairStatusValues.WaitingForParts);
                yield return Transition(RepairStatusValues.WaitingForParts, RepairStatusValues.RepairInProgress);
                yield return Transition(RepairStatusValues.WaitingForParts, RepairStatusValues.ReadyForPickup);
                yield return Transition(RepairStatusValues.RepairInProgress, RepairStatusValues.ReadyForPickup);
                yield return Transition(RepairStatusValues.ReadyForPickup, RepairStatusValues.Completed);
                yield return Transition(RepairStatusValues.Received, RepairStatusValues.Cancelled);
                yield return Transition(RepairStatusValues.Diagnosing, RepairStatusValues.Cancelled);
                yield return Transition(RepairStatusValues.WaitingForApproval, RepairStatusValues.Cancelled);
                yield return Transition(RepairStatusValues.RepairInProgress, RepairStatusValues.Cancelled);
                yield return Transition(RepairStatusValues.WaitingForParts, RepairStatusValues.Cancelled);
                yield return Transition(RepairStatusValues.ReadyForPickup, RepairStatusValues.Cancelled);
            }
        }

        public static IEnumerable<object[]> InvalidTransitions
        {
            get
            {
                yield return Transition(RepairStatusValues.Received, RepairStatusValues.RepairInProgress);
                yield return Transition(RepairStatusValues.Received, RepairStatusValues.ReadyForPickup);
                yield return Transition(RepairStatusValues.Received, RepairStatusValues.Completed);
                yield return Transition(RepairStatusValues.Diagnosing, RepairStatusValues.Received);
                yield return Transition(RepairStatusValues.Diagnosing, RepairStatusValues.ReadyForPickup);
                yield return Transition(RepairStatusValues.WaitingForApproval, RepairStatusValues.Diagnosing);
                yield return Transition(RepairStatusValues.WaitingForApproval, RepairStatusValues.Completed);
                yield return Transition(RepairStatusValues.RepairInProgress, RepairStatusValues.Diagnosing);
                yield return Transition(RepairStatusValues.RepairInProgress, RepairStatusValues.Completed);
                yield return Transition(RepairStatusValues.ReadyForPickup, RepairStatusValues.RepairInProgress);

                foreach (int requestedStatus in NonCompletedStatuses())
                {
                    yield return Transition(RepairStatusValues.Completed, requestedStatus);
                }

                foreach (int requestedStatus in NonCancelledStatuses())
                {
                    yield return Transition(RepairStatusValues.Cancelled, requestedStatus);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ValidTransitions))]
        public void AllowedWorkflowTransition_ReturnsTrue(int previousStatus, int requestedStatus)
        {
            Assert.True(
                RepairStatusTransitionValidator.IsTransitionAllowed(
                    previousStatus,
                    requestedStatus));
        }

        [Theory]
        [InlineData(RepairStatusValues.Received)]
        [InlineData(RepairStatusValues.Diagnosing)]
        [InlineData(RepairStatusValues.WaitingForApproval)]
        [InlineData(RepairStatusValues.RepairInProgress)]
        [InlineData(RepairStatusValues.WaitingForParts)]
        [InlineData(RepairStatusValues.ReadyForPickup)]
        [InlineData(RepairStatusValues.Completed)]
        [InlineData(RepairStatusValues.Cancelled)]
        public void SameValueTransition_ReturnsTrue(int status)
        {
            Assert.True(RepairStatusTransitionValidator.IsTransitionAllowed(status, status));
        }

        [Theory]
        [MemberData(nameof(InvalidTransitions))]
        public void DisallowedWorkflowTransition_ReturnsFalse(int previousStatus, int requestedStatus)
        {
            Assert.False(
                RepairStatusTransitionValidator.IsTransitionAllowed(
                    previousStatus,
                    requestedStatus));
        }

        [Theory]
        [InlineData(999999999, RepairStatusValues.Received)]
        [InlineData(RepairStatusValues.Received, 999999999)]
        [InlineData(999999999, 999999999)]
        public void UnknownChoiceValue_ReturnsFalse(int previousStatus, int requestedStatus)
        {
            Assert.False(
                RepairStatusTransitionValidator.IsTransitionAllowed(
                    previousStatus,
                    requestedStatus));
        }

        private static object[] Transition(int previousStatus, int requestedStatus)
        {
            return new object[] { previousStatus, requestedStatus };
        }

        private static IEnumerable<int> NonCompletedStatuses()
        {
            yield return RepairStatusValues.Received;
            yield return RepairStatusValues.Diagnosing;
            yield return RepairStatusValues.WaitingForApproval;
            yield return RepairStatusValues.RepairInProgress;
            yield return RepairStatusValues.WaitingForParts;
            yield return RepairStatusValues.ReadyForPickup;
            yield return RepairStatusValues.Cancelled;
        }

        private static IEnumerable<int> NonCancelledStatuses()
        {
            yield return RepairStatusValues.Received;
            yield return RepairStatusValues.Diagnosing;
            yield return RepairStatusValues.WaitingForApproval;
            yield return RepairStatusValues.RepairInProgress;
            yield return RepairStatusValues.WaitingForParts;
            yield return RepairStatusValues.ReadyForPickup;
            yield return RepairStatusValues.Completed;
        }
    }
}
