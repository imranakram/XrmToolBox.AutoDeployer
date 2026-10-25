# AutoDeployer test bench

Test components and files for trying AutoDeployer against a real Dataverse environment, by hand
(the [test plan](#test-plan) below) or through the integration tests in `XrmToolBox.AutoDeployer.Tests`.

`XrmToolBox.AutoDeployer.Tests` has two kinds of tests:

| Kind | Needs | Runs in CI |
|---|---|---|
| Unit tests | Nothing | Yes |
| Integration tests (category `Integration`) | A Dataverse environment, a connection string, the components below | No |

Without a connection string the integration tests are **skipped**, not failed, so a fresh clone builds
and tests fine. Web resources are only covered by the manual test plan, because that watcher runs through
the XrmToolBox host.

## Setting up a test environment

You only do this once per environment.

### 1. A Dataverse online environment
A developer or trial environment you don't mind changing. It has to be online: plugin packages don't
exist on-premises.

The test components use the publisher prefix `biz`. Create a publisher with unique name **Biznamics** and
prefix **biz** in that environment (make.powerapps.com → Solutions → Publishers), or `Setup-Dataverse.ps1`
won't find it.

### 2. An app registration with a client secret
In the Azure portal → **Microsoft Entra ID → App registrations → New registration**. Any name, single tenant,
no redirect URI. Then **Certificates & secrets → New client secret**.

Copy the secret's **Value** right away (it's only shown once). The **Secret ID** next to it is not the secret.
Also note the **Application (client) ID** on the Overview page.

### 3. An application user in the environment
In the [Power Platform admin center](https://admin.powerplatform.microsoft.com) → your environment →
**Settings → Users + permissions → Application users → New app user**. Pick the app registration from
step 2, any business unit, and the **System Administrator** security role (System Customizer is enough to
register plugins and web resources, but in a test environment the simplest choice avoids surprises).

### 4. The connection string
One line, with your environment URL (no organization name in the path) and the values from step 2:

```
AuthType=ClientSecret;Url=https://<org>.crm<n>.dynamics.com;ClientId=<application id>;ClientSecret=<secret value>
```

Put it in **one** of these places. The environment variable wins when both are set.

- **`Tests\connection.local.txt`** (recommended). Create the file next to this folder with only that line in it.
  It's gitignored: check with `git check-ignore -v Tests\connection.local.txt`, which should print the
  `.gitignore` rule.
- **`AUTODEPLOYER_TEST_CONNECTION`** environment variable. For the current PowerShell window only:
  ```powershell
  $env:AUTODEPLOYER_TEST_CONNECTION = 'AuthType=ClientSecret;Url=...;ClientId=...;ClientSecret=...'
  ```
  Or for your Windows user, so Visual Studio sees it too (restart Visual Studio after setting it):
  ```powershell
  [Environment]::SetEnvironmentVariable('AUTODEPLOYER_TEST_CONNECTION', 'AuthType=ClientSecret;...', 'User')
  ```

Never commit a connection string, and don't paste it into issues or PRs.

### 5. Check the connection
From `Tests\TestBench`:

```powershell
.\Scripts\Check-Dataverse.ps1
```

No output means the connection works and nothing is registered yet. An error means the connection
string or the app setup is wrong (see [Troubleshooting](#troubleshooting)).

### 6. Build and register the test components
From `Tests\TestBench`:

```powershell
dotnet build .\PluginAssembly -c Debug
dotnet build .\PluginPackage -c Debug
.\Scripts\Setup-Dataverse.ps1
```

`Setup-Dataverse.ps1` creates an unmanaged solution **AutoDeployTest** with the test plugin assembly, the
plugin package and 3 web resources. It's safe to run again. `Check-Dataverse.ps1` should now list 5 rows.

## Running the tests

Build the solution first (Visual Studio, or `msbuild XrmToolBox.AutoDeployer.sln -restore` from the repo root).

**Visual Studio:** Test → Test Explorer. The integration tests are under the trait **Category: Integration**.
Run everything, or right-click that group to run only those.

**Command line**, from the repo root:

```powershell
$vstest = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -products * -find "Common7\IDE\Extensions\TestPlatform\vstest.console.exe" | Select-Object -First 1
$tests = "Tests\XrmToolBox.AutoDeployer.Tests\bin\Debug\XrmToolBox.AutoDeployer.Tests.dll"

& $vstest $tests /TestCaseFilter:"TestCategory!=Integration"   # unit tests, like CI
& $vstest $tests /TestCaseFilter:"TestCategory=Integration"    # integration tests
```

The integration tests take about 20 seconds. Each one deploys a temp copy of a test file, so your working
copies here aren't touched.

## Troubleshooting

| You see | Cause |
|---|---|
| Integration tests **skipped**: "No connection string" | Neither `Tests\connection.local.txt` nor `AUTODEPLOYER_TEST_CONNECTION` is set (Visual Studio started before the variable was set?) |
| Skipped: "Build the test bench first" | Run the two `dotnet build` commands from step 6 |
| Skipped: "Test components not found" | Run `Setup-Dataverse.ps1` (step 6) against the same environment |
| `AADSTS7000215: Invalid client secret` | The Secret ID was copied instead of the Value, or the secret expired |
| `AADSTS700016: Application ... was not found` | Wrong ClientId, or the app is in another tenant than the environment |
| 404 on `.../XRMServices/2011/Organization.svc` | The URL has the organization name in the path, or `AuthType` isn't `ClientSecret` |
| `The user is not a member of the organization` | No application user for the app in that environment (step 3) |
| `Principal user ... is missing prv...` | The application user's security role is too limited (step 3) |
| `Setup-Dataverse.ps1`: publisher not found | Create the **Biznamics** / `biz` publisher (step 1) |

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

The test components are only there to be deployed over. Never point these files at real plugin assemblies or
packages in the environment (in imransdev: Rappen.Canary365, Innofactor, Cinteros).

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

Start XrmToolBox, open AutoDeployer, connect to your test environment (below: **imransdev**).
Start with an empty list (remove anything left from earlier tests).
The sample projects expect the repo at `C:\Github\XrmToolBox.AutoDeployer`; edit the paths in a copy if yours is elsewhere.

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
