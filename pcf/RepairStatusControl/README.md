# Repair Status PCF control

`RepairShop.RepairStatusControl` is a read-only Power Apps Component Framework
(PCF) field control for the model-driven Case form. PCF is the supported framework
for building reusable TypeScript controls that participate in the Power Apps
runtime and are deployed as Dataverse solution components.

The control binds to `incident.cr1a3_repairstatus` and presents seven horizontal
repair stages. Completed stages use a check mark and “Complete” text, the current
stage uses a numbered outlined marker and “Current” text, and future stages use
numbered neutral markers and “Upcoming” text. Unknown and unset values display a
safe textual fallback. Narrow forms retain a readable horizontal layout with
scrolling rather than crushing stage labels.

## Confirmed and manual metadata

The following values are shared intentionally with the existing C# constants:

| Status | Choice integer |
| --- | ---: |
| Received | `702670000` |
| Diagnosing | `702670001` |
| Waiting For Approval | `702670002` |
| Repair In Progress | `702670003` |
| Waiting for Parts | `702670004` |
| Ready For Pickup | `702670005` |
| Completed | `702670006` |
| Cancelled | `702670007` |

Cancelled is displayed as a terminal status and is not inserted into the normal
progress sequence.

## Project structure

```text
pcf/RepairStatusControl/
  RepairStatusControl/
    ControlManifest.Input.xml
    index.ts
    statusModel.ts
    css/RepairStatusControl.css
    strings/RepairStatusControl.1033.resx
  tests/statusModel.test.ts
  RepairStatusControl.pcfproj
  package.json
```

`statusModel.ts` owns the Choice mapping and progress-state calculation. `index.ts`
contains only PCF lifecycle and DOM rendering concerns. No Web API feature or
external service is declared.

## Build and test

Prerequisites are a supported Node.js LTS release and Microsoft Power Platform
CLI. From this directory:

```powershell
npm install
npm run lint
npm test
npm run build
```

Build output is generated under `out/controls/RepairStatusControl`. Generated,
dependency, and build directories are ignored by git.

For the local PCF test harness, run:

```powershell
npm start
```

Use the harness property panel to supply a known Repair Status integer. The
harness validates rendering and resizing, but it does
not replace testing on a model-driven Case form.

## Add to a Dataverse solution

No Power Platform solution project or publisher identity currently exists in this
repository. Use the publisher already approved for the Repair Shop solution; do
not invent another prefix. From a separate solution-project directory:

```powershell
pac solution init --publisher-name <existing-publisher-name> --publisher-prefix <existing-prefix>
pac solution add-reference --path ..\pcf\RepairStatusControl
dotnet build --configuration Release
```

Adjust the relative path for the chosen solution directory. Import the generated
Release solution zip through `make.powerapps.com`, add it to source control if it
will be the repository's ALM solution, and publish all customizations. For rapid
development only, `pac pcf push --publisher-prefix <existing-prefix>` can deploy
the control directly; managed solution packages should be used for higher
environments.

## Put the control on the Case form

1. In `make.powerapps.com`, select the target environment and open the Repair Shop
   unmanaged solution.
2. Confirm the Case Repair Status Choice logical name is
   `cr1a3_repairstatus` and its values match the confirmed values above.
3. Import/add the PCF solution component, then open **Tables > Case > Forms** and
   edit the required main form.
4. Select the Repair Status field and add the **Repair Progress** component.
5. Bind `repairStatus` to the existing Repair Status field
   (`cr1a3_repairstatus`).
6. Enable the component for the intended model-driven clients/form factors. Keep
   the underlying field on the form because it supplies the bound value.
7. Save, publish, open a Case, and verify the seven progression statuses,
   Cancelled, and an unset value.

The control never calls `notifyOutputChanged` and `getOutputs()` returns an empty
object because it is intentionally read-only. Users change Repair Status through
the app's established commands or processes, not through this visualization.

## Accessibility

The stages are an ordered list with a descriptive group label. The current item
uses `aria-current="step"`; each item exposes both its stage and state. Visible
status text and marker shapes prevent color-only communication. There are no
interactive elements, so the control adds no unnecessary tab stops or keyboard
handlers. Forced-colors styling supports Windows high-contrast modes.

## Interview Talking Points

- PCF was chosen over form JavaScript because it is a reusable, supported visual
  field component with a manifest, lifecycle, accessibility surface, and solution
  deployment model. Form scripting is better reserved for form orchestration,
  not a reusable renderer.
- `init` establishes the stable container, `updateView` renders current context
  values, `getOutputs` returns no changes for this read-only control, and `destroy`
  clears owned DOM resources.
- `repairStatus` is `bound` because it is associated with the Dataverse Choice
  column. The control reads but never writes the bound value.
- The framework supplies the current field value through
  `context.parameters.repairStatus.raw`; a Web API retrieve would duplicate data
  already present in the form context and add latency/failure modes.
- PCF controls are packaged and transported through Dataverse solutions, allowing
  the same managed ALM practices as tables, forms, choices, and plug-in steps.
- The server-side C# plug-ins remain authoritative for business behavior: BPF
  changes synchronize Repair Status and completion validates/stamps Case data.
  This PCF control observes that status and presents it; it does not duplicate or
  bypass server-side rules.
