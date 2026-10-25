using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace XrmToolBox.AutoDeployer
{
    internal class WatchPluginFile : IDisposable
    {
        #region Private Fields

        private Control owner;
        private IOrganizationService service;

        // Set by Dispose. A change handler that's still waiting for the DLL checks it, so a removed watch
        // never uploads (possibly to the connection it was created for).
        private volatile bool disposed;

        #endregion Private Fields

        #region Public Constructors

        public WatchPluginFile(string filename, IOrganizationService Service, Control Owner)
        {
            owner = Owner;
            service = Service;
            FullPath = filename;
            File = System.IO.Path.GetFileName(filename);
            Path = System.IO.Path.GetDirectoryName(filename);
            Status = "Watching";
            Log = $"Started at {DateTime.Now}\r\n";
            IsWebResource = IsWebResourceFile(File);
            if (IsWebResource)
            {
                WebResourceId = GetWebResourceId(Service);
            }
            else
            {
                PluginId = GetAssemblyId(Service);
            }
            if ((IsWebResource && WebResourceId != Guid.Empty) || (!IsWebResource && PluginId != Guid.Empty))
            {
                Watcher = new FileSystemWatcher();
                Watcher.Path = Path;
                Watcher.Filter = File;
                Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName;
                Watcher.EnableRaisingEvents = true;
                Watcher.Changed += Plugin_Changed;
                Watcher.Created += Plugin_Changed;
            }
            ListItem = new ListViewItem();
            ListItem.Tag = this;
            UpdateList();
        }

        #endregion Public Constructors

        #region Public Events

        public event EventHandler Changed;

        #endregion Public Events

        #region Public Properties

        public string File { get; private set; }
        public DateTime FileUpdated { get; set; }
        public string FullPath { get; private set; }
        public ListViewItem ListItem { get; private set; }
        public string Log { get; set; }
        public string Path { get; private set; }
        public Guid PluginId { get; private set; }
        public DateTime PluginUpdated { get; set; }
        public string Status { get; private set; }
        public FileSystemWatcher Watcher { get; }
        public Guid WebResourceId { get; private set; }

        #endregion Public Properties

        #region Private Properties

        private bool IsWebResource { get; set; }

        #endregion Private Properties

        #region Public Methods

        public void Dispose()
        {
            disposed = true;

            // Watcher is null when the assembly/web resource was not found in Dataverse
            if (Watcher == null)
            {
                return;
            }
            Watcher.EnableRaisingEvents = false;
            Watcher.Changed -= Plugin_Changed;
            Watcher.Created -= Plugin_Changed;
            Watcher.Dispose();
        }

        #endregion Public Methods

        #region Protected Methods

        protected virtual void OnChanged()
        { Changed?.Invoke(this, EventArgs.Empty); }

        #endregion Protected Methods

        #region Private Methods

        private Guid GetAssemblyId(IOrganizationService Service)
        {
            if (Service == null)
            {
                return Guid.Empty;
            }

            var assembly     = Assembly.Load(ReadFile(FullPath));
            var assemblyName = assembly.GetName();
            // Major.Minor as "1.2" and prefix "1.2." to avoid matching "1.23"
            var majorMinor   = assemblyName.Version.ToString(2);
            var versionPrefix = majorMinor + ".";

            var query = new QueryExpression("pluginassembly");
            query.Criteria.AddCondition("name", ConditionOperator.Equal, assemblyName.Name);

            // Match versions that begin with Major.Minor. (e.g. "1.2." -> "1.2.0.0" or "1.2.3.4"),
            // or equal to "1.2" just in case.
            var versionFilter = new FilterExpression(LogicalOperator.Or);
            versionFilter.AddCondition("version", ConditionOperator.BeginsWith, versionPrefix);
            versionFilter.AddCondition("version", ConditionOperator.Equal, majorMinor);
            query.Criteria.AddFilter(versionFilter);

            return Service.RetrieveMultiple(query).Entities.FirstOrDefault()?.Id ?? Guid.Empty;
        }

        private Guid GetWebResourceId(IOrganizationService Service)
        {
            if (Service == null)
            {
                return Guid.Empty;
            }
            var name = File;
            // Try to find by name (case-insensitive)
            var query = new QueryExpression("webresource");
            query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
            var entity = Service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return entity?.Id ?? Guid.Empty;
        }

        private bool IsWebResourceFile(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".js" || ext == ".html" || ext == ".css" || ext == ".xml" || ext == ".png" || ext == ".jpg" || ext == ".gif";
        }

        private void Plugin_Changed(object sender, FileSystemEventArgs e)
        {
            // Waiting for plugin/webresource become fully available for reading
            while (true)
            {
                if (disposed)
                {
                    return;
                }
                try
                {
                    using (var stream = System.IO.File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream != null)
                        {
                            break;
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                Thread.Sleep(500);
            }

            var file = e.FullPath;
            try
            {
                var lastWriteTime = System.IO.File.GetLastWriteTime(file);
                if (lastWriteTime != FileUpdated)
                {
                    FileUpdated = lastWriteTime;
                    Status = "Updating...";
                    Log += DateTime.Now.ToString("HH:mm:ss.fff") + $" File updated\r\n";
                    UpdateList();

                    // Removed while waiting for the file: don't upload
                    if (disposed)
                    {
                        return;
                    }

                    if (IsWebResource)
                    {
                        UpdateWebResource(file);
                    }
                    else
                    {
                        var plugin = new Entity("pluginassembly", PluginId);
                        plugin["content"] = Convert.ToBase64String(ReadFile(file));
                        service.Update(plugin);
                    }

                    PluginUpdated = DateTime.Now;
                    Status = "Update ok";
                    Log += DateTime.Now.ToString("HH:mm:ss.fff") + (IsWebResource ? " Dataverse web resource updated\r\n" : " Dataverse plugin updated\r\n");
                    UpdateList();
                }
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
                UpdateList();
            }
        }

        private void PublishWebResource(Guid webResourceId)
        {
            var request = new OrganizationRequest("PublishXml");
            var xml = $"<importexportxml><webresources><webresource>{webResourceId.ToString("D").ToUpperInvariant()}</webresource></webresources></importexportxml>";
            request["ParameterXml"] = xml;
            service.Execute(request);
        }

        private byte[] ReadFile(string fileName)
        {
            byte[] buffer = null;
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                buffer = new byte[fs.Length];
                fs.Read(buffer, 0, (int)fs.Length);
            }
            return buffer;
        }

        private void UpdateList()
        {
            MethodInvoker mi = delegate
            {
                while (ListItem.SubItems.Count < 5)
                {
                    ListItem.SubItems.Add(string.Empty);
                }
                ListItem.Text = File;
                ListItem.SubItems[1].Text = Path;
                ListItem.SubItems[2].Text = FileUpdated.Ticks != 0 ? FileUpdated.ToString("HH.mm:ss.fff") : string.Empty;
                ListItem.SubItems[3].Text = PluginUpdated.Ticks != 0 ? PluginUpdated.ToString("HH:mm:ss.fff") : string.Empty;
                ListItem.SubItems[4].Text = Status;
                OnChanged();
            };
            if (owner.InvokeRequired)
            {
                owner.Invoke(mi);
            }
            else
            {
                mi();
            }
        }

        private void UpdateWebResource(string file)
        {
            if (WebResourceId == Guid.Empty) return;
            var webResource = new Entity("webresource", WebResourceId);
            webResource["content"] = Convert.ToBase64String(ReadFile(file));
            service.Update(webResource);
            PublishWebResource(WebResourceId);
        }

        #endregion Private Methods
    }
}