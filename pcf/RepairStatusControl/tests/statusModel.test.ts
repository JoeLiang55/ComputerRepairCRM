import { strict as assert } from "node:assert";
import { test } from "node:test";
import { createRepairProgress, RepairStatusValues } from "../RepairStatusControl/statusModel";

const knownStatuses: ReadonlyArray<readonly [string, number, number]> = [
    ["Received", RepairStatusValues.Received, 0],
    ["Diagnosing", RepairStatusValues.Diagnosing, 1],
    ["Waiting for Approval", RepairStatusValues.WaitingForApproval, 2],
    ["Repair in Progress", RepairStatusValues.RepairInProgress, 3],
    ["Waiting for Parts", RepairStatusValues.WaitingForParts, 4],
    ["Ready for Pickup", RepairStatusValues.ReadyForPickup, 5],
    ["Completed", RepairStatusValues.Completed, 6],
];

for (const [label, value, expectedIndex] of knownStatuses) {
    test(`${label} maps to the correct current stage`, () => {
        const result = createRepairProgress(value);

        assert.equal(result.isMapped, true);
        assert.equal(result.stages[expectedIndex].label, label);
        assert.equal(result.stages[expectedIndex].state, "current");
        assert.match(result.accessibleStatus, new RegExp(`Repair status: ${label}`));
        result.stages.slice(0, expectedIndex).forEach((stage) => {
            assert.equal(stage.state, "completed");
        });
        result.stages.slice(expectedIndex + 1).forEach((stage) => {
            assert.equal(stage.state, "future");
        });
    });
}

test("unknown Choice value returns a safe fallback", () => {
    const result = createRepairProgress(-1);

    assert.equal(result.isMapped, false);
    assert.equal(result.accessibleStatus, "Repair status is not recognized.");
    assert.ok(result.stages.every((stage) => stage.state === "future"));
});

test("null Choice value returns an unset fallback", () => {
    const result = createRepairProgress(null);

    assert.equal(result.isMapped, false);
    assert.equal(result.accessibleStatus, "Repair status is not set.");
    assert.ok(result.stages.every((stage) => stage.state === "future"));
});

test("Cancelled is terminal and is not inserted into normal progress", () => {
    const result = createRepairProgress(RepairStatusValues.Cancelled);

    assert.equal(result.isMapped, true);
    assert.equal(result.isCancelled, true);
    assert.equal(result.accessibleStatus, "Repair status: Cancelled. The repair process has ended.");
    assert.equal(result.stages.some((stage) => stage.label === "Cancelled"), false);
    assert.ok(result.stages.every((stage) => stage.state === "inactive"));
});
