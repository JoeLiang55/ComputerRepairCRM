/**
 * Confirmed Dataverse cr1a3_repairstatus Choice integers.
 */
export const RepairStatusValues = {
    Received: 702670000,
    Diagnosing: 702670001,
    WaitingForApproval: 702670002,
    RepairInProgress: 702670003,
    WaitingForParts: 702670004,
    ReadyForPickup: 702670005,
    Completed: 702670006,
    Cancelled: 702670007,
} as const;

export type StageState = "completed" | "current" | "future" | "inactive";

export interface RepairStageViewModel {
    readonly label: string;
    readonly state: StageState;
}

export interface RepairProgressViewModel {
    readonly stages: readonly RepairStageViewModel[];
    readonly accessibleStatus: string;
    readonly isMapped: boolean;
    readonly isCancelled: boolean;
}

interface RepairStageDefinition {
    readonly label: string;
    readonly value: number;
}

export function createRepairProgress(currentValue: number | null): RepairProgressViewModel {
    const stages: readonly RepairStageDefinition[] = [
        { label: "Received", value: RepairStatusValues.Received },
        { label: "Diagnosing", value: RepairStatusValues.Diagnosing },
        { label: "Waiting for Approval", value: RepairStatusValues.WaitingForApproval },
        { label: "Repair in Progress", value: RepairStatusValues.RepairInProgress },
        { label: "Waiting for Parts", value: RepairStatusValues.WaitingForParts },
        { label: "Ready for Pickup", value: RepairStatusValues.ReadyForPickup },
        { label: "Completed", value: RepairStatusValues.Completed },
    ];

    const isCancelled = currentValue === RepairStatusValues.Cancelled;

    const currentIndex = currentValue === null || isCancelled
        ? -1
        : stages.findIndex((stage) => stage.value === currentValue);
    const isMapped = currentIndex >= 0 || isCancelled;

    return {
        stages: stages.map((stage, index) => ({
            label: stage.label,
            state: isCancelled ? "inactive" : getStageState(index, currentIndex),
        })),
        accessibleStatus: isCancelled
            ? "Repair status: Cancelled. The repair process has ended."
            : isMapped
            ? `Repair status: ${stages[currentIndex].label}. Step ${currentIndex + 1} of ${stages.length}.`
            : currentValue === null
                ? "Repair status is not set."
                : "Repair status is not recognized.",
        isMapped,
        isCancelled,
    };
}

function getStageState(index: number, currentIndex: number): StageState {
    if (currentIndex < 0 || index > currentIndex) {
        return "future";
    }

    return index === currentIndex ? "current" : "completed";
}
