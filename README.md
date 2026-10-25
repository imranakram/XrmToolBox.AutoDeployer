# Auto Deployer Tool in [XrmToolBox](https://www.xrmtoolbox.com)

Auto Deployer is an **XrmToolBox plugin** that watches local files and automatically deploys updates to **Dataverse / Dynamics 365**.

It currently supports:

- **Plugin Assembly auto-update** (watch a `.dll`, update the plugin assembly when the file changes)
- **Web Resource auto-deploy** (watch selected local files and update/publish matching Dataverse Web Resources)
- **Plugin Package auto-update (Online only)** (watch a `.nupkg`, upload it to a selected Dataverse Plugin Package)

---

## Features

### ✅ Plugin Assembly auto-deploy
- Watch one or more plugin assembly `.dll` files
- When the file changes, the tool updates the corresponding Plugin Assembly in Dataverse
- Logs actions and results in the tool output pane

### ✅ Web Resource auto-deploy
Configure:
- **Root folder** (local build output folder)
- **Prefix** (Dataverse web resource prefix, e.g. `cint_`)
- **Patterns** (file globs like `scripts\*.js`)
- **Publish after update** (on/off)
- **Debounce** (ms) to batch rapid rebuilds

Workflow:
- **Scan** local folder to discover files that match the patterns
- The tool auto-generates `CrmName` as: `{prefix}/{relativePath}`
- The grid is **not meant to be edited directly** (except the **Watch** checkbox)
- Watch only mappings where **Watch = true**
- Upload updated content on change, and optionally publish

### ✅ Plugin Package auto-deploy (Online only)
- Watch a Dataverse **Plugin Package** `.nupkg`
- Select the target Plugin Package record from Dataverse
- On file change, the tool uploads the `.nupkg` to the package file column (`pluginpackage.package`)
- This menu/feature is **not available for on-prem** connections

---

## Requirements

- XrmToolBox (latest recommended)
- A Dataverse / Dynamics 365 connection with permissions to:
  - Update Plugin Assemblies (plugin assembly mode)
  - Update Web Resources + Publish (web resource mode)
  - **(Online)** Update Plugin Packages (plugin package mode)

---

## Getting Started

### 1) Install / Run in XrmToolBox
- Build the project
- Copy the plugin output to your XrmToolBox Plugins folder (or use your normal XrmToolBox dev workflow)
- Start XrmToolBox and open **Auto Deployer**

### 2) Connect to an environment
The tool is connection-aware. Configuration is stored **per connection**.

---

## Using Plugin Assembly Watching

1. Click **Add** → **Plugin Assembly**
2. Select a plugin `.dll`
3. The tool starts watching the file
4. Rebuild your project → when the `.dll` changes, Auto Deployer updates the plugin assembly
5. Select an item in the list to see detailed logs

> Tip: Keep your build output stable (same path) so the watcher doesn’t need to be reconfigured.

---

## Using Web Resource Watching

1. Click **Add** → **Web Resources**
2. Configure:
   - **Root folder**  
     Example: `C:\Git\MySolution\dist\gdpr`
   - **Prefix**  
     Example: `cint_`
   - **Patterns** (one per line)  
     Examples:
     ```
     scripts\*.js
     content\*.css
     images\*.png
     ```
   - **Publish after update** (recommended ON)
   - **Debounce** (default 1500ms is a good start)

3. Click **Scan**
   - Adds matching local files to the grid (without duplicates)
   - Updates `CrmName` for existing rows based on the current Prefix
   - Auto-generates `CrmName` as: `{prefix}/{relativePath}`

4. Set **Watch = true** for the rows you want actively monitored

5. Click **Validate**
   - Checks local settings (duplicates, missing files)
   - If connected, checks whether the `CrmName` exists in Dataverse
   - Highlights cells:
     - **Red** = local error (duplicates / missing file) or **not found in Dataverse**
     - **Green** = OK

6. Click **Save**
   - Configuration is saved for the current connection
   - Watchers are created for rows where **Watch = true**

---

## How Web Resource Mapping Works

For each watched mapping:

- **RelativePath** is the local path relative to Root folder  
  Example: `scripts\my_script.js`

- **CrmName** is the Dataverse Web Resource name derived from Prefix  
  Example: `cint_/scripts/my_script.js`

On file change, the tool:
1. Uploads the new content to that Web Resource
2. Optionally publishes (if enabled)
3. Logs result to the main log pane

> Note: The grid columns are intentionally read-only (except Watch).  
> If the Prefix was wrong earlier, use **Scan** or **Validate** after correcting Prefix to rebuild names.

---

## Using Plugin Package Watching (Online only)

1. Click **Add** → **Plugin Package** (only shown for Dataverse online)
2. Select a `.nupkg` file on disk
3. Select the target **Plugin Package** from the environment
4. The tool starts watching the file
5. Rebuild/pack → when the `.nupkg` changes, Auto Deployer uploads the new package content
6. Select an item in the list to see detailed logs

---

## Project Files

Use **Save...** to store everything you're watching (plugin assemblies, plugin packages and web resources with
their root folder and mappings) in an `.adproj` file, and **Open...** to load it again, later or on another
connection.

- When something is already being watched, you're asked whether to **keep** it (the project is added) or
  **remove** it (the project replaces it)
- If a plugin package id from the project doesn't exist in the connected environment, the package is looked up
  by name on the first upload
- Plugin packages are skipped on on-premises connections

---

## Configuration & Persistence

Settings are stored using the standard XrmToolBox settings mechanism:

- Saved **per connection** (so each environment has its own config): plugin assemblies, plugin packages and web resources
- Loaded automatically when the connection changes
- Watchers are re-created when you connect (based on saved config)

---

## Troubleshooting

### “Not connected to an environment”
Connect in XrmToolBox first. Deployment requires an active service connection.

### Nothing happens when files change
- Ensure the row has **Watch = true**
- Check that **Root folder** is correct and file really exists under it
- Some build processes trigger multiple change events; try increasing **Debounce**
- Verify permissions to update web resources / publish / plugin assembly / plugin package

### Web resources show “Not found in Dataverse”
Use **Validate** while connected:
- It will check existence in Dataverse and highlight missing ones
- If Prefix is wrong, fix Prefix then click **Scan** or **Validate** again

### Plugin Package menu missing
Plugin Package deploy is **Online only**. It will be hidden for on-prem connections.

---

## Roadmap / Ideas

- Bulk enable/disable watch flags
- Better summary view in MainControl (active watchers / config overview)

---

## Contributing

PRs and issues are welcome.

If you contribute:
- Keep changes backward compatible where possible
- Prefer small PRs focused on a single improvement
- Include short notes in the PR description on how to test

### Building and testing
- Clone with submodules (`git clone --recursive`, or `git submodule update --init` afterwards)
- Build `XrmToolBox.AutoDeployer.sln`. A Debug build puts the plugin in `bin\Debug\Plugins`, next to an
  `XrmToolBox.exe` you can start for debugging
- `Tests\XrmToolBox.AutoDeployer.Tests` has unit tests, which CI runs on every pull request, and integration
  tests against a real Dataverse environment, which only run locally once you've set up a connection string,
  publisher and prefix in `Tests\testbench.local.json`
- How to set up the test environment and those settings, run the tests, and the manual test plan:
  [Tests/TestBench/README.md](https://github.com/imranakram/XrmToolBox.AutoDeployer/blob/master/Tests/TestBench/README.md)

---

## License

MIT
