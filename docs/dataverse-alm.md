# Dataverse solution ALM

`Solutions/` is the PAC CLI unpacked source for the Dataverse solution whose unique name is `CaseManagementPoC`. `CaseManagementPoC.cdsproj` deliberately uses the same identity and contains the PAC-generated project reference to `pcf/RepairStatusControl/RepairStatusControl.pcfproj`. The repository and CI package an unmanaged solution only; deployment is intentionally out of scope.

## Local unmanaged package

From the repository root:

```powershell
dotnet build RepairShop.Plugins/RepairShop.Plugins/RepairShop.Plugins.csproj --configuration Release
./scripts/Prepare-DataverseSolution.ps1
./scripts/Validate-DataverseSolution.ps1 -PluginDll RepairShop.Plugins/RepairShop.Plugins/bin/Release/net462/RepairShop.Plugins.dll
New-Item -ItemType Directory -Force artifacts | Out-Null
dotnet build CaseManagementPoC.cdsproj --configuration Release
Copy-Item bin/Release/CaseManagementPoC.zip artifacts/CaseManagementPoC.zip -Force
./scripts/Validate-SolutionPackage.ps1 -PackageZip artifacts/CaseManagementPoC.zip -PluginDll RepairShop.Plugins/RepairShop.Plugins/bin/Release/net462/RepairShop.Plugins.dll
```

The preparation script reads the PAC-generated plug-in metadata to locate the assembly destination, copies the exact build output there, and compares SHA-256 hashes. Validation rejects unexpected solution identity, component counts, system SDK steps, missing folders, assembly drift, and a missing or incomplete `RepairShop.RepairStatusControl` package. Building the `.cdsproj` is required because its PAC-created project reference compiles and injects the PCF component; a direct `pac solution pack --folder Solutions` does not build project references.

The supported command used to create the reference is:

```powershell
pac solution add-reference --path pcf/RepairStatusControl/RepairStatusControl.pcfproj
```

The resulting solution import registers the PCF component in the target Dataverse environment; a separate `pac pcf push` is not required. The project reference does not bind the control to a form field. To render it on the Case form, configure `RepairShop.RepairStatusControl` on the intended Choice column in a source environment, include that form customization in `CaseManagementPoC`, and synchronize the solution source again.

## Synchronizing from Dataverse

Export the unmanaged `CaseManagementPoC` solution, then synchronize it with PAC CLI. Do not copy raw archive XML into `Solutions/`.

```powershell
pac solution unpack --zipfile artifacts/CaseManagementPoC-export.zip --folder Solutions --packagetype Unmanaged --allowDelete --allowWrite --clobber
./scripts/Clean-UnpackedSolution.ps1
```

The cleanup is deterministic: it retains only step files whose plug-in type starts with `RepairShop.Plugins.`, removes matching stale root-component/dependency references, and preserves the six images nested under those steps. The structural conversion itself remains PAC-generated.

## Managed packaging strategy

Do not change `<Managed>` in solution XML. When a managed artifact is needed, first export matching unmanaged and managed ZIPs from the build Dataverse environment (PAC expects the managed file alongside the unmanaged file with the `_managed.zip` suffix), and unpack them as a pair:

```powershell
pac solution unpack --zipfile artifacts/CaseManagementPoC-export.zip --folder Solutions --packagetype Both --allowDelete --allowWrite --clobber
dotnet build CaseManagementPoC.cdsproj --configuration Release -p:SolutionPackageType=Both
```

That paired source is the prerequisite for adding managed output to CI. Packaging must continue through the referenced `.cdsproj`, rather than direct folder packing, so both artifacts receive the PCF component. Until then, CI publishes only the verified unmanaged ZIP.
