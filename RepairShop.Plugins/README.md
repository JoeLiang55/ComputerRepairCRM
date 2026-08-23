# RepairShop Dataverse plug-ins

A production-style Microsoft Dataverse plug-in solution for a computer repair
model-driven app. It demonstrates synchronous pipeline validation, sparse entity
updates, entity images, tracing, defensive error handling, and isolated business
logic with unit tests.

## Architecture

```text
RepairShop.Plugins/
  RepairShop.Plugins.slnx
  PLUGIN_REGISTRATION.md
  RepairShop.Plugins/
    Model/                  Dataverse logical names and Choice values
    Plugins/Incident/       the three IPlugin entry points
    Services/               testable completion, transition, and stage-mapping logic
    PluginBase.cs           shared context, tracing, and exception boundary
    RepairShop.Plugins.csproj
  RepairShop.Plugins.Tests/ focused xUnit tests with small SDK mocks
```

The existing `RepairShop.Plugins` naming and directory layout were retained to
avoid breaking the assembly identity or current deployment references. Folders
have a concrete responsibility; there are no empty architecture placeholders.

`PluginBase` contains only repeated Dataverse plumbing. It resolves and validates
`IPluginExecutionContext` and `ITracingService`, writes correlation-aware traces,
and converts unexpected exceptions to stable `InvalidPluginExecutionException`
messages. It holds no per-execution state because Dataverse can reuse a plug-in
instance concurrently.

## Plug-ins

`SyncRepairStatusFromBpfStagePlugin` runs after a BPF instance update. Its narrow
Post Image supplies the current `activestageid` and related Case, so the plug-in
does not retrieve the BPF row. It retrieves the Process Stage name and the Case's
current Repair Status, then sends a sparse Case update only when required. Known
stage matching is trimmed and case-insensitive; unknown or incomplete data is
traced and skipped safely.

`CompleteRepairOnStatusChangePlugin` runs before a Case update. On a genuine
transition to the confirmed Completed Choice, it validates Date Received,
stamps Completion Date with `DateTime.UtcNow`, calculates elapsed whole minutes,
and copies Estimated Cost only when Final Cost is empty. It changes the incoming
`Target`, avoiding an additional `IOrganizationService.Update` call.

`PreventInvalidRepairStatusTransitionPlugin` runs synchronously in PreValidation
on Case Repair Status updates. It compares the sparse Target with `PreImage` and
delegates the state-machine rule to `RepairStatusTransitionValidator`. Same-value
updates are ignored; unsupported Choice values, skipped stages, backwards moves,
and moves away from Completed or Cancelled are rejected with a safe status-name
message. The permitted paths include the forward transitions among all five live
BPF stages, Waiting for Parts exceptions, cancellation from every non-terminal status, and
Ready for Pickup to Completed so the completion plug-in can run normally.

This rule is enforced in a server-side plug-in instead of relying only on a
Business Rule or form JavaScript. The server is the common enforcement boundary
for model-driven UI saves, API updates, imports, Power Automate flows,
integrations, and other server-side updates; a client-only rule can be bypassed
by several of those paths.

## Dataverse SDK and framework

The plug-in targets .NET Framework 4.6.2, a supported Dataverse sandbox target,
and references `Microsoft.CrmSdk.CoreAssemblies` 9.0.2.60. Although its package
name is historical, Microsoft currently documents it as the minimal supported
SDK package for .NET Framework plug-ins. `Microsoft.PowerPlatform.Dataverse.Client`
is not needed because this assembly runs inside Dataverse rather than connecting
as an external client. Tests add only xUnit and Moq; no full Dataverse emulator is
needed for these small, deterministic rules.

## Build and test

Install a current .NET SDK, then run from this directory:

```powershell
dotnet restore RepairShop.Plugins.slnx
dotnet build RepairShop.Plugins.slnx --configuration Release --no-restore
dotnet test RepairShop.Plugins.slnx --configuration Release --no-build
```

The signed deployable output is
`RepairShop.Plugins/bin/Release/net462/RepairShop.Plugins.dll`. Do not upload SDK
DLLs beside it. Treat the checked-in signing key as a development/portfolio key;
protect a production key in the ALM system and keep it stable between upgrades.

## How Dataverse plug-ins execute

Dataverse invokes the registered class through `IPlugin.Execute`. The service
provider exposes `IPluginExecutionContext` (message, stage, table, depth, target,
images and IDs), `ITracingService` for sandbox diagnostics, and—when data access
is needed—`IOrganizationServiceFactory`/`IOrganizationService`. Synchronous steps
participate in the request transaction: throwing
`InvalidPluginExecutionException` cancels the operation and shows its safe
message to the caller.

Filtering attributes keep an Update step from being invoked unless a relevant
column was submitted. They do not prove the value changed, so these plug-ins also
compare the Target/Image/current value before applying work.

### Pipeline choices

- **PreValidation** runs before the main database transaction. Transition
  validation uses it to reject an invalid request early, before transaction work
  and before the PreOperation completion logic can execute.
- **PreOperation** runs in the transaction before persistence. Completion uses it
  because changing the same record's `Target` is persisted with the original
  request, avoiding a second update and a recursive pipeline invocation.
- **PostOperation** runs after the core operation. BPF synchronization uses it so
  the active stage has been accepted and the Post Image represents the current
  BPF instance before updating the related Case.

Entity images are transaction snapshots configured on a step. They are cheaper
and more consistent than retrieving the same row. The transition Pre Image
provides the prior Repair Status, the completion Pre Image provides old/effective
Case values, and the BPF Post Image provides the current stage and Case lookup.
Images intentionally include only required attributes.

All steps are synchronous because users need immediate Case consistency and
immediate validation feedback. An asynchronous PostOperation step would suit
non-blocking notifications or integrations where eventual consistency is
acceptable, but it could not safely reject the initiating completion update.

## Recursion and unnecessary updates

The validation step reads Target and Pre Image only and never issues an update.
The completion step modifies `Target` in PreOperation and never calls Update.
The BPF step updates a different table (`incident`) and compares the existing
Repair Status first. Its mapping does not include Completed, so it cannot trigger
completion behavior accidentally. All classes trace `Depth` for diagnostics but
do not reject valid calls based solely on depth; correct message/table/stage checks,
filtering attributes, transition checks, and sparse updates prevent recursion.

## Registration and metadata

See [PLUGIN_REGISTRATION.md](PLUGIN_REGISTRATION.md) for exact step and image
settings. Register the assembly and all three steps in an unmanaged development
solution, test them in a non-production environment, and deploy managed solutions
to higher environments. Update existing assembly/step components rather than
creating duplicates.

The repository now has an unpacked solution project, but its `Entities`,
`Workflows`, option sets, relationships, and root components are empty. It proves
the publisher prefix `cr1a3`, but not table or attribute logical names. Live
Dataverse evidence confirms `incident`, `cr1a3_repairstatus`,
`cr1a3_datereceived`, `cr1a3_estimatedcost`, `cr1a3_finalcost`, and Choice values
`702670000` through `702670007`. Live evidence also confirms Completion Date as
`gsic_completiondate`, Repair Duration as `gsic_repairduration`, and the BPF table
as `gsic_computerrepairprocess`. Repair Duration is a Whole Number / Duration
column and stores whole minutes.

Live metadata confirms the BPF active-stage attribute (`activestageid`), Case
lookup (`bpf_incidentid`), and five BPF stages: Received, Diagnosing, Waiting For
Approval, Repair In Progress, and Ready For Pickup. Waiting for Parts, Completed,
and Cancelled are not BPF stages and are intentionally not mapped. The sync plug-in
resolves the stage through a minimal `processstage.stagename` retrieve by lookup ID;
it never assumes `EntityReference.Name` is populated in the Post Image.

## PCF companion control

The repository also contains a read-only TypeScript repair-progress field control
under `../pcf/RepairStatusControl`. See its
[PCF README](../pcf/RepairStatusControl/README.md) for build, packaging, Case-form
binding, accessibility, and interview guidance. The PCF visualizes the status;
the C# plug-ins remain responsible for server-side synchronization and validation.

## Interview Talking Points

- `IPlugin` is the small Dataverse entry-point contract; production code quickly
  delegates from it to testable business logic.
- The execution pipeline determines transaction timing. PreOperation is ideal
  for changing the same row; PostOperation is appropriate here for reacting to
  the accepted BPF stage.
- PreValidation is the best fit for a pure guard: it rejects invalid input before
  the main database transaction. PreOperation is the better fit when code must
  change the incoming Target as part of that transaction, as the completion
  plug-in does.
- `IPluginExecutionContext` supplies the message, table, stage, sparse Target,
  entity images, user, depth, and correlation identifiers.
- `IOrganizationService` performs Dataverse operations. This project minimizes
  calls and sends only the Case ID plus changed Repair Status.
- `ITracingService` is the supported way to write diagnostic detail from a
  sandboxed plug-in; correlation IDs connect user-facing errors to traces.
- Pre/Post Images provide selected before/after values without redundant reads.
- Filtering attributes reduce invocations, while value comparisons prevent work
  when a submitted value did not actually change.
- Recursion avoidance is designed through PreOperation Target mutation, table
  boundaries, comparisons, and sparse updates—not a blanket `Depth > 1` return.
- Synchronous steps provide immediate consistency and validation; asynchronous
  steps reduce user latency for work that can be eventually consistent.
- Changing Target in PreOperation is preferable to issuing Update on the same
  record because it stays in the original transaction and avoids another pipeline.
