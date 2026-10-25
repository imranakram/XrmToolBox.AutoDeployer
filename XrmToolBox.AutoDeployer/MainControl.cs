namespace XrmToolBox.AutoDeployer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using System.Xml.Serialization;
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
            _pluginPackageSupportChecked = false;
            _pluginPackageSupported = false;

            if (detail == null || newService == null)
            {
                ClearWebResourceWatchers();
                ClearPluginPackageWatchers();
                bAddPackageMenuItem.Visible = false;
                return;
            }

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
            var plugin = new WatchPluginFile(ofdPlugin.FileName, Service, this);
            plugin.Changed += Plugin_Changed;
            listWatching.Items.Add(plugin.ListItem);
            if (listWatching.SelectedItems.Count == 0)
            {
                listWatching.Items[0].Selected = true;
            }
            bDelSelected.Enabled = true;
            ApplyZebraToListWatchingPreserveSelection();
        }

        private void AddPluginPackage(Guid packageId, string packageName, string path)
        {
            var cfg = LoadPackageConfig();

            // Optional: prevent duplicates by PackageId (recommended)
            var existing = cfg.Items.FirstOrDefault(x => x.PackageId == packageId);
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
            if (listWatching.Items.Count > 0)
            {
                if (MessageBox.Show("Keep existing watchers?\n\nYes to keep, No to remove.",
                    "AutoDeployer", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                {
                    listWatching.Items.Clear();
                    //ClearWebResourceWatchers();
                    ClearPluginPackageWatchers();
                }
            }
            foreach (var watch in project.WatchFiles)
            {
                switch (watch.Type)
                {
                    case Type.PluginAssembly:
                        if (!string.IsNullOrWhiteSpace(watch.Path))
                        {
                            var plugin = new WatchPluginFile(watch.Path, Service, this);
                            plugin.Changed += Plugin_Changed;
                            listWatching.Items.Add(plugin.ListItem);
                        }
                        break;

                    case Type.WebResource:
                        // WebResource watches are managed via the WebResourcesManagerDialog
                        break;

                    case Type.PluginPackage:
                        AddPluginPackage(watch.PackageId, watch.PackageName, watch.Path);
                        break;
                }
            }
            if (project.WatchFiles.Any(x => x.Type == Type.WebResource))
            {
                MessageBox.Show("We are sorry, but WebResource watches are not supported in the configuration file. Please use the Web Resources Manager dialog to manage WebResource watches.", "AutoDeployer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                //RefreshWebResourceWatchers();
            }
            if (project.WatchFiles.Any(x => x.Type == Type.PluginPackage))
            {
                RefreshPluginPackageWatchers();
            }
            bDelSelected.Enabled = listWatching.SelectedItems.Count > 0;
            ApplyZebraToListWatchingPreserveSelection();
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
            var removedWrRelPaths = new List<string>();
            var removedPkgIds = new List<Guid>();

            for (int i = listWatching.SelectedIndices.Count - 1; i >= 0; i--)
            {
                int idx = listWatching.SelectedIndices[i];
                var item = listWatching.Items[idx];

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
                        Type = Type.PluginAssembly
                    });
                }
                else if (item.Tag is WatchWebResourceFile wr)
                {
                    project.WatchFiles.Add(new WatchFile
                    {
                        Path = wr.FullPath,
                        Type = Type.WebResource
                    });
                }
                else if (item.Tag is WatchPluginPackageFile pp)
                {
                    project.WatchFiles.Add(new WatchFile
                    {
                        Path = pp.FullPath,
                        Type = Type.PluginPackage,
                        PackageId = pp.PluginPackageId,
                        PackageName = pp.PluginPackageName,
                    });
                }
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
            var serializer = new XmlSerializer(typeof(Project));

            using (var reader = new StreamReader(fileName))
            {
                try
                {
                    return (Project)serializer.Deserialize(reader);
                }
                catch (Exception ex)
                {
                    ShowErrorDialog(ex, "Load Project");
                    return null;
                }
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

        private void SavePackageConfig(PluginPackageWatchConfig cfg)
        {
            var name = GetPackageSettingsName();
            SettingsManager.Instance.Save(GetType(), cfg, name + ".packages");
        }

        private void SaveProjectXml(Project project, string fileName)
        {
            var serializer = new XmlSerializer(typeof(Project));

            using (var writer = new StreamWriter(fileName))
            {
                try
                {
                    serializer.Serialize(writer, project);
                }
                catch (Exception ex)
                {
                    ShowErrorDialog(ex, "Save Project");
                }
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

                        Service.RetrieveMultiple(qe);
                        a.Result = true;
                    }
                    catch
                    {
                        a.Result = false;
                    }
                },
                PostWorkCallBack = a =>
                {
                    _pluginPackageSupported = a.Error == null && a.Result is bool b && b;

                    bAddPackageMenuItem.Visible = _pluginPackageSupported;

                    if (_pluginPackageSupported)
                    {
                        RefreshPluginPackageWatchers();
                    }
                    else
                    {
                        ClearPluginPackageWatchers();
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