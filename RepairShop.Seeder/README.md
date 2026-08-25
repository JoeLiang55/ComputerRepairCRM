# RepairShop.Seeder

`RepairShop.Seeder` is a standalone .NET 8 console application for creating
realistic development data in Dataverse. It is not a plug-in, is not deployed to
Dataverse, and has no project reference to the production plug-in or PCF code.

The utility creates Contacts, Devices, Cases, Inventory items, and Repair Parts.
Lookups form this chain:

```text
Contact -> Device -> Case -> Repair Part -> Inventory
```

Inventory quantities are sample data only. The seeder does not reserve,
decrement, reorder, or otherwise implement inventory business logic.

## Prerequisites

- .NET 8 SDK
- A non-production Dataverse development environment
- A Microsoft account with appropriate Dataverse privileges
- Existing Contact, Device, Case, Inventory, and Repair Part tables/columns
- Optional writable Text columns of at least 60 characters if automated cleanup
  is required

The authoritative XrmToolBox exports are `../case_dataverse.xlsx`,
`../device_dataverse.xlsx`, `../inventory_dataverse.xlsx`, and
`../repairpart_dataverse.xlsx`. Their exported logical names and types are
compiled in `Schema/DataverseSchema.cs`; they are not environment configuration.
The exports do not contain lookup target metadata. The Incident export confirms
the `gsic_device` Device lookup.

## Configuration

The checked-in `appsettings.json` contains safe defaults and no placeholders.
Create an ignored `appsettings.Local.json` beside it and override the environment-
specific values. Objects are merged recursively, so the local file only needs to
contain changed settings.

For local interactive authentication, optionally put the environment URL in the
checked-in `appsettings.json` or the ignored local file. If it is empty, the
seeder prompts for it:

```json
{
  "Dataverse": {
    "Url": "https://orgc68493d4.crm.dynamics.com"
  }
}
```

Run a seed command and complete the Microsoft sign-in window. Interactive mode
does not require you to create an Azure app registration or configure a client
ID or client secret.

For unattended or application-user authentication, a complete ServiceClient
connection string can still be placed in `appsettings.Local.json`:

```json
{
  "Dataverse": {
    "ConnectionString": "AuthType=ClientSecret;Url=https://orgc68493d4.crm.dynamics.com;ClientId=YOUR-APP-ID;ClientSecret=YOUR-SECRET;RequireNewInstance=true"
  },
  "Schema": {
    "ContactSeedBatch": "",
    "CaseSeedBatch": "",
    "DeviceSeedBatch": "",
    "InventorySeedBatch": "",
    "RepairPartSeedBatch": ""
  }
}
```

The current exports do not contain Seed Batch columns. These five settings are
optional and empty by default. Seeding continues without them, but automated
cleanup is unavailable. If all five are configured, the application writes the
same batch tag to every row and enables `--clear`. A live metadata preflight runs
before writing data and checks that tables and columns exist, configured Seed
Batch columns are Text, types are compatible, and relationship lookups target the
expected table.

Authentication settings are selected in this order:

1. `DATAVERSE_CONNECTION_STRING`
2. `Dataverse:ConnectionString` from `appsettings.Local.json`
3. Interactive OAuth using `Dataverse:Url`, prompting for the URL when empty

The environment connection string is passed directly to `ServiceClient` and
overrides local JSON:

```powershell
$env:DATAVERSE_CONNECTION_STRING = 'AuthType=ClientSecret;Url=https://YOUR-ORG.crm.dynamics.com;ClientId=YOUR-APP-ID;ClientSecret=YOUR-SECRET;RequireNewInstance=true'
```

Never commit a client secret. `appsettings.Local.json` and `.env` files are
already excluded by the repository `.gitignore`.

### Expected column types

| Data | Dataverse type |
| --- | --- |
| Names, manufacturer, model, serial, SKU, seed batch | Text |
| Warranty expiry, received, completion | Date and Time |
| Contact, Device, Case, Inventory relationships | Lookup |
| Priority and Repair Status | Choice |
| Estimated Cost, Final Cost, Unit Cost | Currency |
| Repair Duration, stock quantity, part quantity | Whole Number |

Standard Contact and Case fields plus all custom fields proven by the XrmToolBox
exports are compiled shared constants rather than configuration. The confirmed
Case schema is `incident`, `prioritycode`,
`cr1a3_repairstatus`, `cr1a3_datereceived`, `cr1a3_estimatedcost`,
`cr1a3_finalcost`, `gsic_completiondate`, and `gsic_repairduration`.

## Commands

```powershell
dotnet run --project RepairShop.Seeder.csproj -- --seed
dotnet run --project RepairShop.Seeder.csproj -- --clear
dotnet run --project RepairShop.Seeder.csproj -- --contacts 50
dotnet run --project RepairShop.Seeder.csproj -- --cases 100
dotnet run --project RepairShop.Seeder.csproj -- --inventory
dotnet run --project RepairShop.Seeder.csproj -- --cases 100 --inventory
```

- `--seed` creates the configured default counts for the full relationship graph.
- `--contacts N` creates exactly N Contacts.
- `--cases N` creates N Cases plus a supporting Contact/Device pool. It does not
  attach demo Cases to unrelated existing customer data.
- `--inventory` creates the configured default number of Inventory items.
- Combining `--cases N --inventory` also creates Repair Part associations.
- `--clear` is the only command that deletes data and is enabled only when every
  Seed Batch setting is configured.

Default counts and an optional repeatable random seed can be changed under
`Defaults` in configuration.

## Cleanup safety

When all five optional Seed Batch columns are configured, every created row
receives this tag:

```text
RepairShop.Seeder:<batch-guid>
```

`--clear` queries only rows whose tag begins with `RepairShop.Seeder:`. It deletes
children before parents and never queries untagged records for deletion. Deletion
requires typing the exact word `CLEAR` interactively; redirected/non-interactive
input is rejected.

If any Seed Batch setting is empty, seeding prints a warning and continues, while
`--clear` is disabled before a Dataverse connection is attempted.

When tagging is enabled, records created before a partial failure retain their
batch tag and can be removed with a later confirmed `--clear` run.

## Dataverse access setup

1. Ensure your Microsoft account has access to the development environment.
2. Assign a development-only security role. It needs metadata read access and
   organization-level Create, Read, Delete, Append, and Append To privileges for
   Contact, Device, Case, Inventory, and Repair Part. Add privileges required by
   any synchronous plug-ins that execute during Case creation.
3. Optionally add/export Seed Batch Text columns for every seeded table and put
   those five logical names in `appsettings.Local.json` to enable `--clear`.
   Standard and exported logical names are not configured.
4. Set `Dataverse:Url` or enter it when prompted, then run
   `dotnet run --project RepairShop.Seeder.csproj -- --contacts 1`. Complete the
   Microsoft login window. The
   metadata preflight will report any incorrect mapping before record creation.
5. Run `--seed`, inspect the generated data in the model-driven app, and use
   `--clear` when the demo data is no longer needed.

For application-user automation, register an application, add it as a Dataverse
application user, and supply its full connection string through either of the
two higher-priority sources above.

Microsoft documentation:

- [Register an application and create a Dataverse application user](https://learn.microsoft.com/power-apps/developer/data-platform/walkthrough-register-app-azure-active-directory)
- [Manage application users](https://learn.microsoft.com/power-platform/admin/manage-application-users)
- [Use Dataverse connection strings](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect)
- [ServiceClient interactive OAuth sample](https://github.com/microsoft/PowerApps-Samples/tree/master/dataverse/orgsvc/CSharp-NETCore/ServiceClient)
- [Dataverse security concepts](https://learn.microsoft.com/power-apps/developer/data-platform/security-concepts)

## Build

```powershell
dotnet build RepairShop.Seeder.slnx --configuration Release
```

The executable output is under `bin/Release/net8.0/`.
