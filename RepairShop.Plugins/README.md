# RepairShop Dataverse plug-ins

Production-oriented server-side plug-ins for the Computer Service Repair CRM
model-driven app.

## Before building

Confirm the custom Case column logical names in
`RepairShop.Plugins/Model/IncidentSchema.cs`. The sample uses the publisher
prefix `crs_`:

| Display name | Logical name | Recommended Dataverse type |
| --- | --- | --- |
| Repair Status | `crs_repairstatus` | Choice |
| Completion Date | `crs_completiondate` | Date and Time, User Local |
| Estimated Cost | `crs_estimatedcost` | Currency |
| Final Cost | `crs_finalcost` | Currency |
| Date Received | `crs_datereceived` | Date and Time, User Local; Business Required |
| Repair Duration | `crs_repairduration` | Whole Number, Duration format |

The completed Choice integer is environment/solution metadata, not its label.
Look up that integer in the solution and place it in the step's unsecure
configuration. Do not hard-code a guessed option value.

## Folder structure

```text
RepairShop.Plugins/
├── Configuration/
│   └── PluginConfiguration.cs
├── Model/
│   └── IncidentSchema.cs
├── Plugins/
│   └── Incident/
│       └── CompleteRepairOnStatusChangePlugin.cs
├── RepairShop.Plugins.csproj
└── RepairShop.Plugins.snk
```

The plug-in class is a thin event handler, registration configuration is parsed
separately, and all Dataverse logical names are centralized. For a larger
solution, replace `IncidentSchema` with generated early-bound table classes or
generated field constants, and keep each plug-in focused on one business event.

## Plug-in Registration Tool settings

Register `RepairShop.Plugins.dll`, then register one step for
`RepairShop.Plugins.Plugins.Incident.CompleteRepairOnStatusChangePlugin`:

| Setting | Value |
| --- | --- |
| Message | `Update` |
| Primary Entity | `incident` (Case) |
| Filtering Attributes | `crs_repairstatus` only |
| Event Pipeline Stage | `PreOperation` |
| Execution Mode | `Synchronous` |
| Execution Order | `20` (adjust deliberately if other Case steps exist) |
| Run in User's Context | Calling User |
| Deployment | Server |
| Isolation Mode | Sandbox |
| Assembly location | Database |
| Unsecure Configuration | `completedStatusValue=100000002` (replace with the real integer) |
| Secure Configuration | Empty; this setting is not a secret |

Register this image on the step. Select only the listed columns; never use the
default all-columns image.

| Image type | Name/alias | Columns |
| --- | --- | --- |
| Pre Image | `PreImage` | `crs_repairstatus`, `crs_completiondate`, `crs_estimatedcost`, `crs_finalcost`, `crs_datereceived`, `crs_repairduration` |

PreOperation is intentional: the plug-in adds only the necessary completion
columns to the incoming sparse Target. Dataverse persists them in the original
transaction, so there is no second `Update` call and therefore no plug-in
recursion. The code logs `Depth` for diagnostics but does not make business logic
depend on it, because Depth can legitimately be greater than one in other call
paths.

A Post Image is not appropriate for this same-row mutation because it is not
available until PostOperation and using it would require a second Case update.
Use a narrowly configured Post Image in a separate PostOperation step when a
downstream action truly needs the final committed row values—for example, an
asynchronous integration event that does not modify the same Case.

## Behavior and error handling

The handler exits without a service call unless Repair Status truly transitions
from a different value to Completed. On transition it:

1. stamps Completion Date with `DateTime.UtcNow`;
2. copies Estimated Cost only when Final Cost is null;
3. stores elapsed whole minutes in Repair Duration;
4. adds only changed completion columns to the original sparse Target.

Missing Date Received, a future Date Received, invalid step configuration, and
missing images raise a user-readable `InvalidPluginExecutionException`, causing
the synchronous transaction to roll back. Unexpected exception details are sent
to `ITracingService`; the user receives a stable message containing the
correlation ID. Enable plug-in trace logging in the environment while testing and
use exceptions-only logging (or the organization's approved observability policy)
in production.

## Build and deploy

Build Release and upload only `RepairShop.Plugins.dll` from
`bin/Release/net462`. The SDK assemblies are supplied by Dataverse and should not
be registered with the plug-in assembly. Keep the assembly and step in the same
unmanaged solution in development, then deploy through managed solutions in
higher environments. Update the existing assembly/step during ALM deployments;
do not create duplicate steps.

Treat the included signing key as a development/portfolio key. A production ALM
pipeline should protect its signing key and keep the same key when updating the
assembly so the assembly identity remains stable.

## Minimum verification scenarios

Before promoting the solution, test these paths in a non-production environment:

| Scenario | Expected result |
| --- | --- |
| A non-repair Case column changes | Step is not invoked |
| Repair Status changes to a non-Completed choice | Trace-only early return |
| Completed is submitted when already Completed | Trace-only early return |
| Transition to Completed with Final Cost populated | Date and duration set; Final Cost preserved |
| Transition to Completed with only Estimated Cost | Estimated Cost copied to Final Cost |
| Transition with no Date Received | Friendly error; original status update rolls back |
| Transition with Date Received in the future | Friendly error; original status update rolls back |
