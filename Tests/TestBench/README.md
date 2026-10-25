# AutoDeployer test bench

Test components and files for trying AutoDeployer against a real Dataverse environment, by hand
(the test plan below) or through the integration tests in `XrmToolBox.AutoDeployer.Tests`.
They are set up for a dev environment with a publisher **Biznamics** (prefix `biz`).

## Connection

The scripts and the integration tests read a connection string from the `AUTODEPLOYER_TEST_CONNECTION`
environment variable, or else from `Tests\connection.local.txt`. That file is gitignored. Never commit a
connection string.

```
AuthType=ClientSecret;Url=https://<org>.crm<n>.dynamics.com;ClientId=<app id>;ClientSecret=<secret>
```

## First time

From `Tests\TestBench`:

```powershell
dotnet build .\PluginAssembly -c Debug
dotnet build .\PluginPackage -c Debug
.\Scripts\Setup-Dataverse.ps1
```

## Automated tests

`XrmToolBox.AutoDeployer.Tests` has unit tests (run in CI) and integration tests (category `Integration`,
local only). The integration tests deploy temp copies of these files and are skipped when no connection
string is set, or when the components above aren't registered. Web resources are only covered by the
test plan below, since that watcher needs the XrmToolBox host. Run them from the repo root with VSTest:

```powershell
vstest.console.exe Tests\XrmToolBox.AutoDeployer.Tests\bin\Debug\XrmToolBox.AutoDeployer.Tests.dll /TestCaseFilter:"TestCategory=Integration"
```

## What's here

| Folder | What |
|---|---|
| `PluginAssembly\` | Classic plugin assembly `AutoDeployTest.Plugins` (registered, 1 plugin type, no steps) |
| `PluginPackage\` | Plugin package, registered as `biz_AutoDeployTest.Package` |
| `WebResources\` | Root folder for 3 web resources, registered as `biz_/autodeploytest/...` |
| `Projects\` | Sample `.adproj` files (see below) |
| `Scripts\` | `Setup-Dataverse.ps1`, `Cleanup-Dataverse.ps1`, `Bump.ps1`, `Check-Dataverse.ps1` |

Everything in Dataverse lives in the unmanaged solution **AutoDeployTest** (publisher Biznamics, prefix `biz`).
`Scripts\Cleanup-Dataverse.ps1` removes all of it.

The test components are only there to be deployed over. The real assembly and packages in the environment
(Rappen.Canary365, Innofactor, Cinteros) should never be watched with these files.

## Sample projects

| File | Use |
|---|---|
| `AutoDeployTest.adproj` | Everything: assembly, package and 3 web resources |
| `AutoDeployTest-WrongPackageId.adproj` | Package with an id that doesn't exist, right name (tests lookup by name) |
| `AutoDeployTest-Legacy.adproj` | Format saved by the first version of PR 19 (web resources as plain paths) |
| `AutoDeployTest-OtherRoot.adproj` | Web resources under a different root folder (tests the "replace settings?" prompt) |

## Helpers

```powershell
.\Scripts\Bump.ps1 Assembly      # change + rebuild the DLL
.\Scripts\Bump.ps1 Package       # change + rebuild the .nupkg
.\Scripts\Bump.ps1 WebResource   # change scripts\test.js
.\Scripts\Check-Dataverse.ps1    # last modified time of each test component in Dataverse
```

A deploy has landed when `Check-Dataverse.ps1` shows a newer ModifiedOn for that component,
and the row in AutoDeployer shows `Update ok`.

## Test plan

Start XrmToolBox, open AutoDeployer, connect to **imransdev**.
Start with an empty list (remove anything left from earlier tests).

### 1. Open a project
1. Open… `AutoDeployTest.adproj`.
2. Expect 5 rows: the assembly, `PKG: biz_AutoDeployTest.Package` and 3 web resources.
   The status bar should show `Web resources: 3 active (of 3) — Root: C:\Github\XrmToolBox.AutoDeployer\Tests\TestBench\WebResources`.
3. Run each `Bump.ps1` option and check the row shows `Update ok` and `Check-Dataverse.ps1` shows a new time.

### 2. Reload per connection (new: assemblies too)
1. With the list from test 1, close the AutoDeployer tab and open it again (or reconnect to imransdev).
2. Expect the same 5 rows back without opening the project. Before this change the assembly would be missing.
3. `Bump.ps1 Assembly` and check it still deploys.
4. Connect to another environment (if you have one): the list should switch to that connection's watches.
   Connect back to imransdev: the 5 rows come back.

### 3. Remove selected
1. Select the assembly row and click Remove. Reconnect.
2. Expect the assembly to stay gone (it's forgotten for this connection).
3. `Bump.ps1 Assembly`: nothing should deploy (Check-Dataverse time unchanged).

### 4. Replace watchers ("No") — the watcher leak fix
1. Open `AutoDeployTest.adproj` so all 5 rows are there.
2. Open… `AutoDeployTest-Legacy.adproj` and answer **No** (remove existing).
3. Expect only the assembly row, plus the message that web resources from an earlier version can't be restored.
4. `Bump.ps1 WebResource` and `Bump.ps1 Package`: nothing should deploy. Before the fix, the removed
   watchers kept deploying in the background.
5. Reconnect: still just the assembly (package and web resource watches were forgotten).

### 5. Keep watchers ("Yes") + different web resource root
1. Open `AutoDeployTest.adproj` (answer No to start clean).
2. Open `AutoDeployTest-OtherRoot.adproj` and answer **Yes** (keep).
3. Expect a question about replacing the web resource settings (root folder differs).
   - **No**: the 3 web resources stay as they are.
   - **Yes**: you end up with one web resource (`test.js`) under the `scripts` root. It should still deploy.
4. Opening the same project twice with **Yes** should not create duplicate rows.

### 6. Package id that doesn't exist here
1. Remove the package row if it's there.
2. Open `AutoDeployTest-WrongPackageId.adproj`.
3. `Bump.ps1 Package`.
4. Expect `Update ok` and a log line like `Package id 11111111-... not found, using 'biz_AutoDeployTest.Package' (7c2c3f3a-...) found by name`.
5. Save… the project somewhere and open the file: the PackageId should now be `7c2c3f3a-67d0-f111-a05b-7ced8d3ebe2f`.
6. Optional failure case: edit a copy of the file so PackageName is also wrong. The upload should fail with
   "does not exist in this environment. Remove it and add it again…".

### 7. Open right after connecting (support check timing)
1. Reconnect to imransdev and click Open… `AutoDeployTest.adproj` immediately.
2. Expect the package to be added once the check finishes, not skipped with the "only supported online" message.

### 8. Guards
1. Without a connection: Open… and Add > Plugin assembly should say "Connect to an environment first."
2. On an on-prem environment (if you have one): opening `AutoDeployTest.adproj` should skip the package with
   the "only supported in Dataverse online" message, and load the rest.

### 9. Save round trip
1. Get a mix of watches in the list, Save… to a new `.adproj`.
2. Answer No and open it again. The same rows should come back, web resources included.

## Cleanup

```powershell
.\Scripts\Cleanup-Dataverse.ps1
```
