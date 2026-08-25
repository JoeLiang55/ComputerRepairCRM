# Plug-in registration

Build `RepairShop.Plugins.slnx` in Release and register
`RepairShop.Plugins/bin/Release/net462/RepairShop.Plugins.dll` with the Plug-in
Registration Tool. Use Database storage and Sandbox isolation. Dataverse provides
the Microsoft SDK assemblies; upload only `RepairShop.Plugins.dll`.

Repair Due Date is configured as the confirmed logical name
`gsic_repairduedate`. The plug-in retains a guard against blank configuration and
the original unresolved sentinel, but this build is ready to register.

## CalculateRepairDueDatePlugin

The repository did not previously use or document Case Priority. This plug-in
uses the standard Dataverse Case attribute `incident.prioritycode` and its default
SDK Choice values: High `1`, Normal `2`, and Low `3`. Confirm those values in the
target environment before registration if the standard Choice has been customized.

### Create

| Setting | Exact value |
| --- | --- |
| Plugin | `RepairShop.Plugins.Plugins.Incident.CalculateRepairDueDatePlugin` |
| Message | `Create` |
| Primary Entity | `incident` |
| Stage | `PreOperation` (20) |
| Mode | `Synchronous` |
| Filtering Attributes | None |
| Image | None |
| Configuration | `none` (leave secure and unsecure configuration empty) |

### Update

| Setting | Exact value |
| --- | --- |
| Plugin | `RepairShop.Plugins.Plugins.Incident.CalculateRepairDueDatePlugin` |
| Message | `Update` |
| Primary Entity | `incident` |
| Stage | `PreOperation` (20) |
| Mode | `Synchronous` |
| Filtering Attributes | `prioritycode,cr1a3_datereceived` |
| Pre Image | Required |
| Pre Image Name | `PreImage` |
| Pre Image Alias | `PreImage` |
| Pre Image Attributes | `prioritycode,cr1a3_datereceived` |
| Post Image | None |
| Configuration | `none` (leave secure and unsecure configuration empty) |

The Create step reads Date Received and Priority from Target. The Update step
merges sparse Target values with `PreImage`, then compares prior and effective
values because filtering attributes prove only that a column was submitted—not
that its value changed. Both steps add the calculated column directly to Target;
neither issues `IOrganizationService.Update`.

Date Received is never replaced with the current time. Missing Date Received on
Create is traced and skipped. Explicitly clearing Date Received on Update clears
Repair Due Date. A missing or unrecognized effective Priority does not produce a
date; on Update, the output is cleared to avoid retaining a misleading SLA.

The configured SLAs are Low = 7, Normal = 5, and High = 2 business days. Saturdays
and Sundays are skipped; holidays are not yet modeled. Time-of-day is preserved.

## PreventInvalidRepairStatusTransitionPlugin

| Setting | Exact value used by this repository |
| --- | --- |
| Assembly | `RepairShop.Plugins.dll` |
| Plug-in class | `RepairShop.Plugins.Plugins.Incident.PreventInvalidRepairStatusTransitionPlugin` |
| Message | `Update` |
| Primary table | `incident` |
| Pipeline stage | `PreValidation` (10) |
| Execution mode | `Synchronous` |
| Filtering attributes | `cr1a3_repairstatus` |
| Execution order | `10` |
| Run in user's context | Calling User |
| Deployment | Server |
| Configuration | `none` (leave secure and unsecure configuration empty) |
| Pre Image Name/Alias | `PreImage` |
| Pre Image Attributes | `cr1a3_repairstatus` |
| Post Image | None |

Expected behavior: every submitted Case Repair Status change is checked against
the server-side transition rules before the main database transaction starts.
PreValidation is appropriate because this step only accepts or rejects input; it
does not need to modify the row inside the transaction. Rejection therefore
happens before core database work and before the completion step.

This step permits the normal forward sequence, the Waiting for Parts exception,
cancellation from any non-terminal repair status, and Ready for Pickup to
Completed. Completed and Cancelled are terminal. Same-value submissions are
accepted without work. Its rules include the four forward transitions among the
five live BPF stages. A BPF attempt to skip or move backwards is rejected by the
same server-side rule, so no change to `SyncRepairStatusFromBpfStagePlugin` is
required.

The transition step's execution order is `10`. The completion step remains order
`20`; more importantly, pipeline stages guarantee that PreValidation transition
checking runs before the PreOperation completion logic. When Ready for Pickup is
changed to Completed, validation succeeds and the existing completion plug-in can
stamp its completion fields normally.

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

Live Dataverse metadata confirms the BPF table `gsic_computerrepairprocess`, active
stage lookup `activestageid`, and Case lookup `bpf_incidentid`. The live BPF stages
are exactly Received, Diagnosing, Waiting For Approval, Repair In Progress, and
Ready For Pickup. Waiting for Parts, Completed, and Cancelled are intentionally
not mapped because they are not BPF stages. Do not register this step on `incident`.

The plug-in does not use `activestageid`'s `EntityReference.Name`, because Dataverse
does not guarantee that lookup display names are populated in entity images. It
retrieves only `processstage.stagename` using the active stage ID, then retrieves
only `incident.cr1a3_repairstatus` to avoid an unnecessary Case update when the
status already matches.

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
