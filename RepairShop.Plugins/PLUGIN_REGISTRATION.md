# Plug-in registration

Build `RepairShop.Plugins.slnx` in Release and register
`RepairShop.Plugins/bin/Release/net462/RepairShop.Plugins.dll` with the Plug-in
Registration Tool. Use Database storage and Sandbox isolation. Dataverse provides
the Microsoft SDK assemblies; upload only `RepairShop.Plugins.dll`.

## SyncRepairStatusFromBpfStagePlugin

| Setting | Exact value used by this repository |
| --- | --- |
| Assembly | `RepairShop.Plugins.dll` |
| Plug-in class | `RepairShop.Plugins.Plugins.Incident.SyncRepairStatusFromBpfStagePlugin` |
| Message | `Update` |
| Primary table | `gsic_computerrepairprocess` |
| Pipeline stage | `PostOperation` (40) |
| Execution mode | `Synchronous` |
| Filtering attributes | `activestageid` |
| Execution order | `20` |
| Run in user's context | Calling User |
| Deployment | Server |
| Unsecure/Secure configuration | Empty |
| Pre Image | None |
| Post Image | Required |
| Image name/alias | `PostImage` |
| Image attributes | `activestageid,bpf_incidentid` |

Expected behavior: after the active stage changes, the step reads the current
stage and Case reference from `PostImage`, retrieves the `processstage.stagename`,
and maps the five established stage names to `incident.cr1a3_repairstatus`. It retrieves only
the Case Repair Status and sends a sparse Case update only when the value differs.
Unknown/missing stages and missing/invalid Case references are traced and skipped.

The BPF instance table `gsic_computerrepairprocess` is confirmed from the live
Dataverse environment. The current code uses `activestageid` for its active-stage
attribute and `bpf_incidentid` for its Case lookup. The unpacked solution has empty
`Entities` and `Workflows` nodes, so those two attributes and the BPF stage list
are not independently verified. Confirm them in live BPF metadata before registering.
Waiting for Parts and Completed are not mapped until actual BPF stages with those
names are confirmed. If metadata differs, update `ComputerRepairProcessSchema.cs`
and `RepairStatusStageMapper.cs`, rebuild, and use the confirmed BPF table logical
name as the Primary table. Do not register this step on `incident`.

## CompleteRepairOnStatusChangePlugin

| Setting | Exact value used by this repository |
| --- | --- |
| Assembly | `RepairShop.Plugins.dll` |
| Plug-in class | `RepairShop.Plugins.Plugins.Incident.CompleteRepairOnStatusChangePlugin` |
| Message | `Update` |
| Primary table | `incident` |
| Pipeline stage | `PreOperation` (20) |
| Execution mode | `Synchronous` |
| Filtering attributes | `cr1a3_repairstatus` |
| Execution order | `20` |
| Run in user's context | Calling User |
| Deployment | Server |
| Unsecure configuration | Empty |
| Secure configuration | Empty |
| Pre Image | Required |
| Image name/alias | `PreImage` |
| Image attributes | `cr1a3_repairstatus,cr1a3_datereceived,cr1a3_estimatedcost,cr1a3_finalcost` |
| Post Image | None |

Expected behavior: only a transition to the confirmed Completed value stamps
UTC Completion Date, calculates elapsed whole minutes, and copies Estimated Cost
when Final Cost is empty. The values are added to the original PreOperation
`Target`; the plug-in does not issue another Case update. Missing or future Date
Received rejects the transaction with a user-readable error.

Completed is compiled as the confirmed Choice value `702670006`. The output
attributes are the confirmed `gsic_completiondate` and `gsic_repairduration`.
Repair Duration must be a Whole Number column using the Duration format; Dataverse
stores the value as whole minutes. Neither output attribute is required in the
Pre Image because the implementation always calculates and writes a new value.

## Choice values compiled into the assembly

| Repair Status | Integer |
| --- | ---: |
| Received | `702670000` |
| Diagnosing | `702670001` |
| Waiting For Approval | `702670002` |
| Repair In Progress | `702670003` |
| Waiting For Parts | `702670004` |
| Ready For Pickup | `702670005` |
| Completed | `702670006` |
| Cancelled | `702670007` |

These values are confirmed against the target Dataverse Repair Status Choice.
Cancelled is terminal and is not mapped as a normal BPF progression stage.
