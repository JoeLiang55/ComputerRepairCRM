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
- An identity with appropriate Dataverse privileges
- Existing Contact, Device, Case, Inventory, and Repair Part tables/columns
- A writable Text column of at least 60 characters on every seeded table for the
  seed batch tag

The repository's unpacked solution does not contain Device, Inventory, or Repair
Part metadata. Their logical names are therefore deliberately not guessed.
Configure the exact logical names from Power Apps before running the utility.

## Configuration

The checked-in `appsettings.json` contains safe defaults and schema placeholders.
Create an ignored `appsettings.Local.json` beside it and override the environment-
specific values. Objects are merged recursively, so the local file only needs to
contain changed settings.

Example:

```json
{
  "Dataverse": {
    "ConnectionString": "AuthType=ClientSecret;Url=https://YOUR-ORG.crm.dynamics.com;ClientId=YOUR-APP-ID;ClientSecret=YOUR-SECRET;RequireNewInstance=true"
  },
  "Schema": {
    "Contact": {
      "SeedBatch": "your_contactseedbatch"
    },
    "Device": {
      "Entity": "your_device",
      "Name": "your_name",
      "Manufacturer": "your_manufacturer",
      "Model": "your_model",
      "SerialNumber": "your_serialnumber",
      "WarrantyExpiry": "your_warrantyexpiry",
      "ContactLookup": "your_contactid",
      "SeedBatch": "your_seedbatch"
    }
  }
}
```

Complete every `REPLACE_WITH_...` entry from `appsettings.json` in the local
override. The application performs a metadata preflight before writing data. It
checks that tables and columns exist, seed batch columns are Text, configured
column types are compatible, and relationship lookups target the expected table.

The connection string can instead be supplied through the environment, which
overrides either JSON file:

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

The confirmed Case schema defaults are `incident`, `prioritycode`,
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
- `--clear` is the only command that deletes data.

Default counts and an optional repeatable random seed can be changed under
`Defaults` in configuration.

## Cleanup safety

Every created row receives a tag in its configured Seed Batch column:

```text
RepairShop.Seeder:<batch-guid>
```

`--clear` queries only rows whose tag begins with `RepairShop.Seeder:`. It deletes
children before parents and never queries untagged records for deletion. Deletion
requires typing the exact word `CLEAR` interactively; redirected/non-interactive
input is rejected.

If a run fails partway through, the records already created retain their batch
tag and can be removed with a later confirmed `--clear` run.

## Dataverse application-user setup

1. Register a single-tenant application in Microsoft Entra ID and create a client
   secret (or use another `ServiceClient`-supported connection string).
2. In Power Platform admin center, open the development environment, then go to
   **Settings > Users + permissions > Application users** and add that app.
3. Assign a development-only security role. It needs metadata read access and
   organization-level Create, Read, Delete, Append, and Append To privileges for
   Contact, Device, Case, Inventory, and Repair Part. Add privileges required by
   any synchronous plug-ins that execute during Case creation.
4. Copy every table and column logical name from Power Apps into
   `appsettings.Local.json`. Do not use display names or schema names.
5. Set `DATAVERSE_CONNECTION_STRING` or the ignored local JSON connection string.
6. Run `dotnet run --project RepairShop.Seeder.csproj -- --contacts 1` first. The
   metadata preflight will report any incorrect mapping before record creation.
7. Run `--seed`, inspect the generated batch in the model-driven app, and use
   `--clear` when the demo data is no longer needed.

Microsoft documentation:

- [Register an application and create a Dataverse application user](https://learn.microsoft.com/power-apps/developer/data-platform/walkthrough-register-app-azure-active-directory)
- [Manage application users](https://learn.microsoft.com/power-platform/admin/manage-application-users)
- [Use Dataverse connection strings](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect)
- [Dataverse security concepts](https://learn.microsoft.com/power-apps/developer/data-platform/security-concepts)

## Build

```powershell
dotnet build RepairShop.Seeder.slnx --configuration Release
```

The executable output is under `bin/Release/net8.0/`.
