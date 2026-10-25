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

Decide which **publisher** the test components belong to. It's an input (step 4):
- **Your own publisher**, e.g. unique name `Biznamics` with prefix `biz`. It's used as it is and never deleted.
- **Nothing set:** the test bench creates its own publisher **AutoDeployTest** with prefix **adt**, and
  `Cleanup-Dataverse.ps1` removes it again.

The prefix ends up in the component names (`<prefix>_AutoDeployTest.Package`, `<prefix>_/autodeploytest/...`),
so the integration tests and the sample projects need it too. Use one prefix per environment at a time: the
plugin assembly and the solution have the same name for every prefix, so run `Cleanup-Dataverse.ps1` with the
old settings before switching.

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

### 4. The settings: connection string, publisher, prefix
Copy `Tests\testbench.example.json` to **`Tests\testbench.local.json`** and fill it in:

```json
{
  "connection": "AuthType=ClientSecret;Url=https://<org>.crm<n>.dynamics.com;ClientId=<application id>;ClientSecret=<secret value>",
  "publisher": "Biznamics",
  "prefix": "biz"
}
```

- **connection**: your environment URL (no organization name in the path) and the values from step 2.
- **publisher** / **prefix**: the publisher's unique name and its customization prefix (step 1). Leave both
  out for the test bench's own `AutoDeployTest` / `adt`. When the publisher exists, its prefix has to match.

`testbench.local.json` is gitignored: `git check-ignore -v Tests\testbench.local.json` should print the
`.gitignore` rule. The scripts and the integration tests read the same file.

Each value can also come from an environment variable, which wins over the file. That's handy for a build
agent, or to try another publisher without editing the file:

| Setting | Environment variable |
|---|---|
| connection | `AUTODEPLOYER_TEST_CONNECTION` |
| publisher | `AUTODEPLOYER_TEST_PUBLISHER` |
| prefix | `AUTODEPLOYER_TEST_PREFIX` |

```powershell
# This PowerShell window only
$env:AUTODEPLOYER_TEST_PREFIX = 'biz'
# For your Windows user, so Visual Studio sees it too (restart Visual Studio afterwards)
[Environment]::SetEnvironmentVariable('AUTODEPLOYER_TEST_PREFIX', 'biz', 'User')
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

`Setup-Dataverse.ps1` creates the publisher if needed, and an unmanaged solution **AutoDeployTest** with the
test plugin assembly, the plugin package and 3 web resources. It also writes the sample projects, filled in
for this clone and environment, to `Projects\Local` (gitignored). It's safe to run again.
`Check-Dataverse.ps1` should now list 5 rows.

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
| Integration tests **skipped**: "No connection string" | No `connection` in `Tests\testbench.local.json` and no `AUTODEPLOYER_TEST_CONNECTION` (Visual Studio started before the variable was set?) |
| Skipped: "Build the test bench first" | Run the two `dotnet build` commands from step 6 |
| Skipped: "Test components with prefix '...' not found" | Run `Setup-Dataverse.ps1` (step 6) with the same environment and prefix as the tests |
| `Publisher '...' has the prefix '...', not '...'` | The `prefix` setting doesn't match the existing publisher |
| `The prefix '...' is already used by publisher '...'` | Set `publisher` to that publisher, or choose another prefix |
| `AADSTS7000215: Invalid client secret` | The Secret ID was copied instead of the Value, or the secret expired |
| `AADSTS700016: Application ... was not found` | Wrong ClientId, or the app is in another tenant than the environment |
| 404 on `.../XRMServices/2011/Organization.svc` | The URL has the organization name in the path, or `AuthType` isn't `ClientSecret` |
| `The user is not a member of the organization` | No application user for the app in that environment (step 3) |
| `Principal user ... is missing prv...` | The application user's security role is too limited (step 3) |

## What's here

| Folder | What |
|---|---|
| `PluginAssembly\` | Classic plugin assembly `AutoDeployTest.Plugins` (registered, 1 plugin type, no steps) |
| `PluginPackage\` | Plugin package, registered as `<prefix>_AutoDeployTest.Package` |
| `WebResources\` | Root folder for 3 web resources, registered as `<prefix>_/autodeploytest/...` |
| `Projects\` | Sample project templates (see below) |
| `Projects\Local\` | The templates filled in by `Setup-Dataverse.ps1` (gitignored): open these |
| `Scripts\` | `Setup-Dataverse.ps1`, `Cleanup-Dataverse.ps1`, `Bump.ps1`, `Check-Dataverse.ps1` |

Everything in Dataverse lives in the unmanaged solution **AutoDeployTest**, under the configured publisher.
`Scripts\Cleanup-Dataverse.ps1` removes all of it, and the publisher too if the test bench created it.

The test components are only there to be deployed over. Never point these files at real plugin assemblies or
packages in the environment, such as another tool's assembly or a third-party managed package.

## Sample projects

The files in `Projects\` are templates with placeholders for the test bench folder, the prefix and the
package id. Open the filled-in copies in **`Projects\Local`**, which `Setup-Dataverse.ps1` writes.

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

Start XrmToolBox, open AutoDeployer, connect to your test environment.
Start with an empty list (remove anything left from earlier tests).
Open the sample projects from `Projects\Local` (run `Setup-Dataverse.ps1` first).

### 1. Open a project
1. Open… `Projects\Local\AutoDeployTest.adproj`.
2. Expect 5 rows: the assembly, `PKG: <prefix>_AutoDeployTest.Package` and 3 web resources.
   The status bar should show `Web resources: 3 active (of 3) — Root: <this folder>\WebResources`.
3. Run each `Bump.ps1` option and check the row shows `Update ok` and `Check-Dataverse.ps1` shows a new time.

### 2. Reload per connection (new: assemblies too)
1. With the list from test 1, close the AutoDeployer tab and open it again (or reconnect).
2. Expect the same 5 rows back without opening the project. Before this change the assembly would be missing.
3. `Bump.ps1 Assembly` and check it still deploys.
4. Connect to another environment (if you have one): the list should switch to that connection's watches.
   Connect back: the 5 rows come back.

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
4. Expect `Update ok` and a log line like `Package id 11111111-... not found, using '<prefix>_AutoDeployTest.Package' (<real id>) found by name`.
5. Save… the project somewhere and open the file: the PackageId should now be the real one (the same as in `Projects\Local\AutoDeployTest.adproj`).
6. Optional failure case: edit a copy of the file so PackageName is also wrong. The upload should fail with
   "does not exist in this environment. Remove it and add it again…".

### 7. Open right after connecting (support check timing)
1. Reconnect and click Open… `AutoDeployTest.adproj` immediately.
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
