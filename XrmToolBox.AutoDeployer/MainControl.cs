namespace XrmToolBox.AutoDeployer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;
    using McTools.Xrm.Connection;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using XrmToolBox.Extensibility;
    using XrmToolBox.Extensibility.Interfaces;

    public partial class MainControl : PluginControlBase, IGitHubPlugin, IWorkerHost, IAboutPlugin
    {
        #region Private Fields

        private static readonly System.Drawing.Color ZebraEven = System.Drawing.Color.White;
        private static readonly System.Drawing.Color ZebraOdd = System.Drawing.Color.FromArgb(245, 245, 245);
        private readonly List<WatchPluginPackageFile> _packageWatchers = new List<WatchPluginPackageFile>();
        private readonly List<WatchWebResourceFile> _webResourceWatchers = new List<WatchWebResourceFile>();

        // Bumped on every connection change, so a plugin package support check that finishes after
        // switching connection knows its result is about the previous one
        private int _connectionGeneration;

        // Packages from an opened project, waiting for the plugin package support check to complete
        private List<WatchFile> _pendingProjectPackages;

        private bool _pluginPackageCheckPending;

        private bool _pluginPackageSupportChecked;

        private bool _pluginPackageSupported;

        #endregion Private Fields

        #region Public Constructors

        public MainControl()
        {
            InitializeComponent();
            listWatching.FullRowSelect = true;
            listWatching.HideSelection = false;
            listWatching.UseCompatibleStateImageBehavior = false;
            listWatching.GridLines = false;
        }

        #endregion Public Constructors

        #region Public Properties

        string IGitHubPlugin.RepositoryName =>
            "XrmToolBox.AutoDeployer";

        string IGitHubPlugin.UserName =>
            "ImranAkram";

        #endregion Public Properties

        #region Public Methods

        public void ShowAboutDialog()
        {
            try
            {
                var about = new About();
                //StartPosition = FormStartPosition.CenterParent
                about.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            // Reset per-connection state
            _connectionGeneration++;
            _pluginPackageSupportChecked = false;
            _pluginPackageSupported = false;

            _pluginPackageCheckPending = false;
            _pendingProjectPackages = null;

            if (detail == null || newService == null)
            {
                ClearPluginAssemblyWatchers();
                ClearWebResourceWatchers();
                ClearPluginPackageWatchers();
                bAddPackageMenuItem.Visible = false;
                return;
            }

            RefreshPluginAssemblyWatchers();
            UpdatePluginPackageUi();     // decides visibility; clears pkg watchers if not supported
            RefreshWebResourceWatchers();

            // Only start package watchers when supported (otherwise you're guaranteed to fail on-prem)
            if (_pluginPackageSupported)
            {
                RefreshPluginPackageWatchers();
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void AddPluginAssembly(string path)
        {
            var alreadyWatched = listWatching.Items.Cast<ListViewItem>()
                .Any(i => i.Tag is WatchPluginFile w && string.Equals(w.FullPath, path, StringComparison.OrdinalIgnoreCase));
            if (alreadyWatched)
            {
                return;
            }

            var error = AddPluginAssemblyWatcher(path);
            if (error != null)
            {
                MessageBox.Show($"Unable to watch {path}:\n{error}", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var cfg = LoadAssemblyConfig();
            if (!cfg.Paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                cfg.Paths.Add(path);
                SaveAssemblyConfig(cfg);
            }

            if (listWatching.SelectedItems.Count == 0)
            {
                listWatching.Items[0].Selected = true;
            }
            bDelSelected.Enabled = true;
            ApplyZebraToListWatchingPreserveSelection();
        }

        // Returns the error when the assembly can't be watched (e.g. the DLL was deleted or moved), otherwise null
        private string AddPluginAssemblyWatcher(string path)
        {
            try
            {
                var plugin = new WatchPluginFile(path, Service, this);
                plugin.Changed += Plugin_Changed;
                listWatching.Items.Add(plugin.ListItem);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private void AddPluginPackage(Guid packageId, string packageName, string path)
        {
            var cfg = LoadPackageConfig();

            // Prevent duplicates by PackageId, or by name + file: a watch whose id was resolved by name
            // (see WatchPluginPackageFile) no longer has the id stored in the project it came from
            var existing = cfg.Items.FirstOrDefault(x => x.PackageId == packageId)
                ?? cfg.Items.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(packageName)
                    && string.Equals(x.PackageName, packageName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.NupkgPath, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsActive = true;
                existing.NupkgPath = path;
                existing.PackageName = packageName;
            }
            else
            {
                cfg.Items.Add(new PluginPackageWatchItem
                {
                    IsActive = true,
                    NupkgPath = path,
                    PackageId = packageId,
                    PackageName = packageName
                });
            }
            SavePackageConfig(cfg);
        }

        private void ApplyProject(Project project)
        {
            if (project == null)
            {
                return;
            }
            // Packages still waiting for the support check count as watched too, so they can be replaced
            if (listWatching.Items.Count > 0 || _pendingProjectPackages?.Count > 0)
            {
                if (MessageBox.Show("Keep existing watchers?\n\nYes to keep, No to remove.",
                    "AutoDeployer", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                {
                    RemoveAllWatchers();
                }
            }

            foreach (var watch in project.WatchFiles.Where(x => x.Type == WatchFileType.PluginAssembly))
            {
                if (!string.IsNullOrWhiteSpace(watch.Path))
                {
                    AddPluginAssembly(watch.Path);
                }
            }

            var packages = project.WatchFiles.Where(x => x.Type == WatchFileType.PluginPackage).ToList();
            if (packages.Count > 0)
            {
                if (_pluginPackageCheckPending)
                {
                    // Support check still running after connecting; added when it completes.
                    // Appended, so a second project opened with "keep" doesn't drop the first one's packages.
                    _pendingProjectPackages = (_pendingProjectPackages ?? new List<WatchFile>()).Concat(packages).ToList();
                }
                else
                {
                    AddProjectPackages(packages);
                }
            }

            if (!string.IsNullOrWhiteSpace(project.WebResources?.RootPath))
            {
                if (MergeWebResourceConfig(project.WebResources))
                {
                    RefreshWebResourceWatchers();
                }
            }
            else if (project.WatchFiles.Any(x => x.Type == WatchFileType.WebResource))
            {
                // Older project files only stored the file path, which is not enough to recreate the watch
                MessageBox.Show("The web resources in this project file can't be restored, since it was saved by an earlier version. Add them again in the Web Resources Manager and save the project.",
                    "AutoDeployer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            bDelSelected.Enabled = listWatching.SelectedItems.Count > 0;
            UpdateWrSummary();
            ApplyZebraToListWatchingPreserveSelection();
        }

        private void AddProjectPackages(List<WatchFile> packages)
        {
            if (!_pluginPackageSupported)
            {
                MessageBox.Show($"The project contains {packages.Count} plugin package(s), but Plugin Packages are only supported in Dataverse online environments. They were skipped.",
                    "AutoDeployer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var watch in packages)
            {
                AddPluginPackage(watch.PackageId, watch.PackageName, watch.Path);
            }
            RefreshPluginPackageWatchers();
        }

        private void ApplyZebraToListWatching()
        {
            for (int i = 0; i < listWatching.Items.Count; i++)
            {
                var item = listWatching.Items[i];
                item.BackColor = (i % 2 == 0) ? ZebraEven : ZebraOdd;
            }
        }

        // Call this after you add/remove items, and after refreshes.
        // It keeps the selected row readable by forcing highlight colors.
        private void ApplyZebraToListWatchingPreserveSelection()
        {
            var selected = listWatching.SelectedIndices.Cast<int>().ToList();
            ApplyZebraToListWatching();

            // re-apply selection (helps if Windows repaints oddly)
            foreach (var i in selected)
            {
                if (i >= 0 && i < listWatching.Items.Count)
                {
                    listWatching.Items[i].Selected = true;
                }
            }
        }

        private void bAddPackageMenuItem_Click(object sender, EventArgs e)
        {
            if (ConnectionDetail == null || Service == null)
            {
                MessageBox.Show("Connect to an environment first.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!_pluginPackageSupported)
            {
                MessageBox.Show("Plugin Packages are only supported in Dataverse online environments.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var ofd = new OpenFileDialog
            {
                Filter = "Dataverse Plugin Package (*.nupkg)|*.nupkg",
                Title = "Select plugin package (.nupkg)"
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (!TrySelectPackageFromDataverse(out var packageId, out var packageName))
                {
                    return;
                }

                AddPluginPackage(packageId, packageName, ofd.FileName);
                RefreshPluginPackageWatchers();
            }
        }

        // Plugin Package are only for Online CRM. Used to hide/show related UI.
        private void bAddPluginMenuItem_Click(object sender, EventArgs e)
        {
            if (ConnectionDetail == null || Service == null)
            {
                MessageBox.Show("Connect to an environment first.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ofdPlugin.ShowDialog() == DialogResult.OK)
            {
                AddPluginAssembly(ofdPlugin.FileName);
            }
        }

        private void bAddWebResourceMenuItem_Click(object sender, EventArgs e)
        {
            if (ConnectionDetail == null)
            {
                MessageBox.Show("Connect to an environment first.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var cfg = LoadWebResourceConfig();

            using (var dialog = new WebResourcesManagerDialog(cfg, Service))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SaveWebResourceConfig(dialog.Config);
                    RefreshWebResourceWatchers();
                }
            }
        }

        private void bDelSelected_Click(object sender, EventArgs e)
        {
            var removedAssemblyPaths = new List<string>();
            var removedWrRelPaths = new List<string>();
            var removedPkgIds = new List<Guid>();

            for (int i = listWatching.SelectedIndices.Count - 1; i >= 0; i--)
            {
                int idx = listWatching.SelectedIndices[i];
                var item = listWatching.Items[idx];

                if (item.Tag is WatchPluginFile plugin)
                {
                    removedAssemblyPaths.Add(plugin.FullPath);
                }

                if (item.Tag is WatchWebResourceFile wr)
                {
                    _webResourceWatchers.Remove(wr);
                    if (!string.IsNullOrWhiteSpace(wr.RelativePath))
                    {
                        removedWrRelPaths.Add(wr.RelativePath);
                    }
                }

                if (item.Tag is WatchPluginPackageFile pp)
                {
                    _packageWatchers.Remove(pp);
                    if (pp.PluginPackageId != Guid.Empty)
                    {
                        removedPkgIds.Add(pp.PluginPackageId);
                    }
                }

                if (item.Tag is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                listWatching.Items.RemoveAt(idx);
            }

            // Persist removal (so they don't come back on reconnect)
            if (removedAssemblyPaths.Count > 0)
            {
                var acfg = LoadAssemblyConfig();
                acfg.Paths.RemoveAll(p => removedAssemblyPaths.Contains(p, StringComparer.OrdinalIgnoreCase));
                SaveAssemblyConfig(acfg);
            }

            if (removedWrRelPaths.Count > 0)
            {
                var cfg = LoadWebResourceConfig();
                cfg.Mappings.RemoveAll(m => removedWrRelPaths
                    .Any(p => string.Equals(p, m.RelativePath, StringComparison.OrdinalIgnoreCase)));
                SaveWebResourceConfig(cfg);
            }

            if (removedPkgIds.Count > 0)
            {
                var pcfg = LoadPackageConfig();
                pcfg.Items.RemoveAll(x => removedPkgIds.Contains(x.PackageId));
                SavePackageConfig(pcfg);
            }

            // Optional: re-sync watchers from config (keeps everything consistent)
            RefreshWebResourceWatchers();
            RefreshPluginPackageWatchers();

            txtLog.Text = string.Empty;
            bDelSelected.Enabled = listWatching.SelectedItems.Count > 0;
            UpdateWrSummary();
            ApplyZebraToListWatchingPreserveSelection();
        }

        private void bOpen_Click(object sender, EventArgs e)
        {
            if (ConnectionDetail == null || Service == null)
            {
                MessageBox.Show("Connect to an environment first.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var ofq = new OpenFileDialog
            {
                Filter = "Auto Deployer Project (*.adproj)|*.adproj",
                Title = "Open Auto Deployer Project"
            })
            {
                if (ofq.ShowDialog(this) == DialogResult.OK)
                {
                    var project = LoadProjectXml(ofq.FileName);
                    ApplyProject(project);
                }
            }
        }

        private void bSave_Click(object sender, EventArgs e)
        {
            using (var sfq = new SaveFileDialog
            {
                Filter = "Auto Deployer Project (*.adproj)|*.adproj",
                Title = "Save Auto Deployer Project"
            })
            {
                if (sfq.ShowDialog(this) == DialogResult.OK)
                {
                    var project = GenerateProject();
                    SaveProjectXml(project, sfq.FileName);
                }
            }
        }

        private void ClearPluginAssemblyWatchers()
        {
            for (int i = listWatching.Items.Count - 1; i >= 0; i--)
            {
                var item = listWatching.Items[i];
                if (item.Tag is WatchPluginFile plugin)
                {
                    plugin.Dispose();
                    listWatching.Items.RemoveAt(i);
                }
            }
            ApplyZebraToListWatchingPreserveSelection();
        }

        private void ClearPluginPackageWatchers()
        {
            for (int i = listWatching.Items.Count - 1; i >= 0; i--)
            {
                var item = listWatching.Items[i];
                if (item.Tag is WatchPluginPackageFile pp)
                {
                    pp.Dispose();
                    listWatching.Items.RemoveAt(i);
                }
            }
            _packageWatchers.Clear();
            ApplyZebraToListWatchingPreserveSelection();
        }

        private void ClearWebResourceWatchers()
        {
            for (int i = listWatching.Items.Count - 1; i >= 0; i--)
            {
                var item = listWatching.Items[i];
                if (item.Tag is WatchWebResourceFile wr)
                {
                    wr.Dispose();
                    listWatching.Items.RemoveAt(i);
                }
            }
            _webResourceWatchers.Clear();
            UpdateWrSummary();
            ApplyZebraToListWatchingPreserveSelection();
        }

        private Project GenerateProject()
        {
            var project = new Project();
            foreach (ListViewItem item in listWatching.Items)
            {
                if (item.Tag is WatchPluginFile plugin)
                {
                    project.WatchFiles.Add(new WatchFile
                    {
                        Path = plugin.FullPath,
                        Type = WatchFileType.PluginAssembly
                    });
                }
                else if (item.Tag is WatchPluginPackageFile pp)
                {
                    project.WatchFiles.Add(new WatchFile
                    {
                        Path = pp.FullPath,
                        Type = WatchFileType.PluginPackage,
                        PackageId = pp.PluginPackageId,
                        PackageName = pp.PluginPackageName,
                    });
                }
            }

            var wrCfg = LoadWebResourceConfig();
            if (!string.IsNullOrWhiteSpace(wrCfg.RootPath) && wrCfg.Mappings?.Count > 0)
            {
                project.WebResources = wrCfg;
            }

            return project;
        }

        private string GetPackageSettingsName()
            => (ConnectionDetail?.ConnectionId ?? Guid.Empty).ToString("D");

        private string GetWebResourceSettingsName()
        {
            // “name” suffix => per connection file
            return (ConnectionDetail?.ConnectionId ?? Guid.Empty).ToString("D");
        }

        private void listWatching_SelectedIndexChanged(object sender, EventArgs e)
        {
            Plugin_Changed(sender, e);
            bDelSelected.Enabled = listWatching.SelectedItems.Count > 0;
            UpdateWrSummary();
        }

        private PluginAssemblyWatchConfig LoadAssemblyConfig()
        {
            var name = GetPackageSettingsName();
            if (SettingsManager.Instance.TryLoad(GetType(), out PluginAssemblyWatchConfig cfg, name + ".assemblies"))
            {
                return cfg;
            }

            return new PluginAssemblyWatchConfig();
        }

        private PluginPackageWatchConfig LoadPackageConfig()
        {
            var name = GetPackageSettingsName();
            if (SettingsManager.Instance.TryLoad(GetType(), out PluginPackageWatchConfig cfg, name + ".packages"))
            {
                return cfg;
            }

            return new PluginPackageWatchConfig();
        }

        private Project LoadProjectXml(string fileName)
        {
            try
            {
                return ProjectFile.Load(fileName);
            }
            catch (Exception ex)
            {
                ShowErrorDialog(ex, "Load Project");
                return null;
            }
        }

        private WebResourceWatchConfig LoadWebResourceConfig()
        {
            var name = GetWebResourceSettingsName();

            if (SettingsManager.Instance.TryLoad(GetType(), out WebResourceWatchConfig cfg, name))
            {
                return cfg;
            }

            return new WebResourceWatchConfig { PublishEnabled = true, DebounceMs = 1500 };
        }

        // Adds the project's web resource mappings to this connection's settings.
        // Returns false if the user declined replacing settings that use a different root folder or prefix.
        private bool MergeWebResourceConfig(WebResourceWatchConfig projectCfg)
        {
            var cfg = LoadWebResourceConfig();
            var result = WebResourceConfigMerger.Merge(cfg, projectCfg);
            switch (result)
            {
                case WebResourceMergeResult.Merged:
                    SaveWebResourceConfig(cfg);
                    return true;

                case WebResourceMergeResult.RootDiffers:
                case WebResourceMergeResult.PrefixDiffers:
                    var what = result == WebResourceMergeResult.RootDiffers ? "root folder" : "prefix";
                    var projectValue = result == WebResourceMergeResult.RootDiffers ? projectCfg.RootPath : projectCfg.Prefix;
                    var currentValue = result == WebResourceMergeResult.RootDiffers ? cfg.RootPath : cfg.Prefix;
                    if (MessageBox.Show($"The project's web resources use the {what}\n{projectValue}\n\nbut this connection uses\n{currentValue}\n\nReplace this connection's web resource settings with the project's?",
                        "AutoDeployer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        return false;
                    }
                    SaveWebResourceConfig(projectCfg);
                    return true;

                default:
                    SaveWebResourceConfig(projectCfg);
                    return true;
            }
        }

        private void Plugin_Changed(object sender, EventArgs e)
        {
            if (listWatching.SelectedItems.Count == 1)
            {
                var tag = listWatching.SelectedItems[0].Tag;
                switch (tag)
                {
                    case WatchPluginFile plugin:
                        txtLog.Text = plugin.Log;
                        return;

                    case WatchWebResourceFile wr:
                        txtLog.Text = wr.Log;
                        return;

                    case WatchPluginPackageFile pp:
                        txtLog.Text = pp.Log;
                        return;
                }
            }

            txtLog.Text = string.Empty;
        }

        private void RemoveAllWatchers()
        {
            foreach (ListViewItem item in listWatching.Items)
            {
                if (item.Tag is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            listWatching.Items.Clear();
            _packageWatchers.Clear();
            _webResourceWatchers.Clear();

            // Packages from a project opened while the support check is running are removed too
            _pendingProjectPackages = null;

            // Forget persisted watches too, otherwise they come back on reconnect
            var acfg = LoadAssemblyConfig();
            acfg.Paths.Clear();
            SaveAssemblyConfig(acfg);

            var pcfg = LoadPackageConfig();
            pcfg.Items.Clear();
            SavePackageConfig(pcfg);

            var wcfg = LoadWebResourceConfig();
            wcfg.Mappings?.Clear();
            SaveWebResourceConfig(wcfg);

            txtLog.Text = string.Empty;
            UpdateWrSummary();
        }

        private void RefreshPluginAssemblyWatchers()
        {
            ClearPluginAssemblyWatchers();

            if (Service == null)
            {
                return;
            }

            // One missing DLL must not stop the other watchers from switching to the new connection,
            // so report failures afterwards. The paths stay saved and come back once the DLL is there again.
            var failures = new List<string>();
            foreach (var path in LoadAssemblyConfig().Paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var error = AddPluginAssemblyWatcher(path);
                    if (error != null)
                    {
                        failures.Add($"{path}\n    {error}");
                    }
                }
            }

            bDelSelected.Enabled = listWatching.SelectedItems.Count > 0;
            ApplyZebraToListWatchingPreserveSelection();

            if (failures.Count > 0)
            {
                MessageBox.Show("These plugin assemblies can't be watched right now and were skipped:\n\n" + string.Join("\n", failures),
                    "AutoDeployer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshPluginPackageWatchers()
        {
            // remove existing package watchers from list + dispose
            for (int i = listWatching.Items.Count - 1; i >= 0; i--)
            {
                var item = listWatching.Items[i];
                if (item.Tag is WatchPluginPackageFile pp)
                {
                    pp.Dispose();
                    listWatching.Items.RemoveAt(i);
                }
            }
            _packageWatchers.Clear();

            var cfg = LoadPackageConfig();
            if (cfg?.Items == null || cfg.Items.Count == 0 || Service == null)
            {
                return;
            }

            foreach (var it in cfg.Items.Where(x => x.IsActive))
            {
                if (string.IsNullOrWhiteSpace(it.NupkgPath) || it.PackageId == Guid.Empty)
                {
                    continue;
                }

                var watcher = new WatchPluginPackageFile(it, Service, this, () => SavePackageConfig(cfg));
                watcher.Changed += Plugin_Changed;
                _packageWatchers.Add(watcher);
                listWatching.Items.Add(watcher.ListItem);
            }

            bDelSelected.Enabled = listWatching.Items.Count > 0;
            ApplyZebraToListWatchingPreserveSelection();
        }

        private void RefreshWebResourceWatchers()
        {
            // Remove existing WR watchers from list + dispose them
            for (int i = listWatching.Items.Count - 1; i >= 0; i--)
            {
                var item = listWatching.Items[i];
                if (item.Tag is WatchWebResourceFile wr)
                {
                    wr.Dispose();
                    listWatching.Items.RemoveAt(i);
                }
            }
            _webResourceWatchers.Clear();

            var cfg = LoadWebResourceConfig();
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.RootPath))
            {
                return;
            }

            if (Service == null)
            {
                MessageBox.Show("Not connected to an environment.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (cfg.Mappings == null || cfg.Mappings.Count == 0)
            {
                return;
            }

            foreach (var mapping in cfg.Mappings)
            {
                if (!mapping.IsActive)
                {
                    continue;
                }

                var watcher = new WatchWebResourceFile(cfg, mapping, Service, this);
                watcher.Changed += Plugin_Changed; // reuse existing log display
                _webResourceWatchers.Add(watcher);
                listWatching.Items.Add(watcher.ListItem);
            }

            bDelSelected.Enabled = listWatching.Items.Count > 0;
            UpdateWrSummary();
            ApplyZebraToListWatchingPreserveSelection();
        }

        private void SaveAssemblyConfig(PluginAssemblyWatchConfig cfg)
        {
            var name = GetPackageSettingsName();
            SettingsManager.Instance.Save(GetType(), cfg, name + ".assemblies");
        }

        private void SavePackageConfig(PluginPackageWatchConfig cfg)
        {
            var name = GetPackageSettingsName();
            SettingsManager.Instance.Save(GetType(), cfg, name + ".packages");
        }

        private void SaveProjectXml(Project project, string fileName)
        {
            try
            {
                ProjectFile.Save(project, fileName);
            }
            catch (Exception ex)
            {
                ShowErrorDialog(ex, "Save Project");
            }
        }

        private void SaveWebResourceConfig(WebResourceWatchConfig cfg)
        {
            var name = GetWebResourceSettingsName();
            SettingsManager.Instance.Save(GetType(), cfg, name);
        }

        private bool TrySelectPackageFromDataverse(out Guid packageId, out string packageName)
        {
            packageId = Guid.Empty;
            packageName = null;

            var qe = new QueryExpression("pluginpackage")
            {
                ColumnSet = new ColumnSet("name")
            };
            qe.Orders.Add(new OrderExpression("name", OrderType.Ascending));

            var results = Service.RetrieveMultiple(qe).Entities
                .Select(e => new { Id = e.Id, Name = e.GetAttributeValue<string>("name") ?? e.Id.ToString() })
                .ToList();

            if (results.Count == 0)
            {
                MessageBox.Show("No plugin packages found in this environment.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            var names = results.Select(r => r.Name).ToList();

            using (var dlg = new SelectItemDialog(
                "Select Plugin Package",
                names,
                "Select the Dataverse Plugin Package to upload into.\r\nDouble-click an item or select it and click OK.", okText: "Select"))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                if (dlg.SelectedIndex < 0 || dlg.SelectedIndex >= results.Count)
                {
                    return false;
                }

                var chosen = results[dlg.SelectedIndex];
                packageId = chosen.Id;
                packageName = chosen.Name;
                return true;
            }
        }

        private void UpdatePluginPackageUi()
        {
            // Hide by default until we know it's supported
            bAddPackageMenuItem.Visible = false;

            if (Service == null)
            {
                return;
            }

            // Avoid spamming checks on every UpdateConnection call
            if (_pluginPackageSupportChecked)
            {
                bAddPackageMenuItem.Visible = _pluginPackageSupported;
                if (!_pluginPackageSupported)
                {
                    ClearPluginPackageWatchers();
                }

                return;
            }

            _pluginPackageSupportChecked = true;
            _pluginPackageCheckPending = true;

            // The check runs against this connection; if it changes before the answer comes, the answer is ignored
            var generation = _connectionGeneration;
            var service = Service;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Updating connection...",
                Work = (w, a) =>
                {
                    try
                    {
                        // If entity doesn't exist (typical on-prem), this throws.
                        var qe = new QueryExpression("pluginpackage")
                        {
                            ColumnSet = new ColumnSet(false),
                            TopCount = 1
                        };

                        service.RetrieveMultiple(qe);
                        a.Result = true;
                    }
                    catch
                    {
                        a.Result = false;
                    }
                },
                PostWorkCallBack = a =>
                {
                    if (generation != _connectionGeneration)
                    {
                        return;
                    }

                    _pluginPackageSupported = a.Error == null && a.Result is bool b && b;
                    _pluginPackageCheckPending = false;

                    bAddPackageMenuItem.Visible = _pluginPackageSupported;

                    if (_pluginPackageSupported)
                    {
                        RefreshPluginPackageWatchers();
                    }
                    else
                    {
                        ClearPluginPackageWatchers();
                    }

                    if (_pendingProjectPackages != null)
                    {
                        var packages = _pendingProjectPackages;
                        _pendingProjectPackages = null;
                        AddProjectPackages(packages);
                    }
                }
            });
        }

        private void UpdateWrSummary()
        {
            var cfg = LoadWebResourceConfig();
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.RootPath))
            {
                tsWrSummary.Text = "Web resources: not configured";
                return;
            }

            var total = cfg.Mappings?.Count ?? 0;
            var active = cfg.Mappings?.Count(m => m.IsActive) ?? 0;

            tsWrSummary.Text =
                $"Web resources: {active} active (of {total}) — Root: {cfg.RootPath} — Prefix: {cfg.Prefix} — " +
                $"Publish: {(cfg.PublishEnabled ? "On" : "Off")} — Debounce: {cfg.DebounceMs}ms";
        }

        #endregion Private Methods
    }
}