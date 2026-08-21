import { IInputs, IOutputs } from "./generated/ManifestTypes";
import { createRepairProgress, RepairStageViewModel } from "./statusModel";

export class RepairStatusControl implements ComponentFramework.StandardControl<IInputs, IOutputs> {
    private container!: HTMLDivElement;

    public init(
        _context: ComponentFramework.Context<IInputs>,
        _notifyOutputChanged: () => void,
        _state: ComponentFramework.Dictionary,
        container: HTMLDivElement,
    ): void {
        this.container = container;
        this.container.classList.add("repair-status-control");
        this.container.setAttribute("role", "group");
        this.container.setAttribute("aria-label", "Repair progress");
    }

    public updateView(context: ComponentFramework.Context<IInputs>): void {
        const viewModel = createRepairProgress(context.parameters.repairStatus.raw);

        const statusText = document.createElement("p");
        statusText.className = viewModel.isMapped
            ? "repair-status-control__status"
            : "repair-status-control__status repair-status-control__status--fallback";
        if (viewModel.isCancelled) {
            statusText.classList.add("repair-status-control__status--cancelled");
        }
        statusText.setAttribute("role", "status");
        statusText.setAttribute("aria-live", "polite");
        statusText.textContent = viewModel.accessibleStatus;

        const progress = document.createElement("ol");
        progress.className = "repair-status-control__progress";
        progress.setAttribute("aria-label", "Repair stages");

        viewModel.stages.forEach((stage, index) => {
            progress.appendChild(this.createStage(stage, index));
        });

        this.container.replaceChildren(statusText, progress);
    }

    public getOutputs(): IOutputs {
        // This control observes a bound field but never changes it.
        return {};
    }

    public destroy(): void {
        // There are no event handlers or external resources to release.
        this.container.replaceChildren();
    }

    private createStage(stage: RepairStageViewModel, index: number): HTMLLIElement {
        const item = document.createElement("li");
        item.className = `repair-status-control__stage repair-status-control__stage--${stage.state}`;
        item.dataset.state = stage.state;
        item.setAttribute("aria-label", `${stage.label}: ${this.getStateLabel(stage.state)}`);
        if (stage.state === "current") {
            item.setAttribute("aria-current", "step");
        }

        const marker = document.createElement("span");
        marker.className = "repair-status-control__marker";
        marker.setAttribute("aria-hidden", "true");
        marker.textContent = stage.state === "completed" ? "✓" : String(index + 1);

        const label = document.createElement("span");
        label.className = "repair-status-control__label";
        label.textContent = stage.label;

        const state = document.createElement("span");
        state.className = "repair-status-control__state";
        state.textContent = this.getStateLabel(stage.state);

        item.append(marker, label, state);
        return item;
    }

    private getStateLabel(state: RepairStageViewModel["state"]): string {
        switch (state) {
            case "completed":
                return "Complete";
            case "current":
                return "Current";
            case "inactive":
                return "Not completed";
            default:
                return "Upcoming";
        }
    }
}
