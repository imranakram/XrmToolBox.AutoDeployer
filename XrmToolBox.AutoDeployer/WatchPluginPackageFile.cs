using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Xrm.Sdk;

namespace XrmToolBox.AutoDeployer
{
    internal sealed class WatchPluginPackageFile : IDisposable
    {
        #region Private Fields

        private const int DebounceMs = 750;

        // tweak if needed
        private readonly Action _persist;

        private readonly SemaphoreSlim _uploadGate = new SemaphoreSlim(1, 1);
        private readonly PluginPackageWatchItem item;
        private readonly Control owner;
        private readonly IOrganizationService service;
        private int _changeVersion;
        private CancellationTokenSource _debounceCts = new CancellationTokenSource(); // can be exchanged to null on dispose
        private int _disposed;

        #endregion Private Fields

        #region Public Constructors

        // 0 = alive, 1 = disposed
        public WatchPluginPackageFile(PluginPackageWatchItem watchItem, IOrganizationService svc, Control ownerControl, Action persist)
        {
            item = watchItem ?? throw new ArgumentNullException(nameof(watchItem));
            owner = ownerControl ?? throw new ArgumentNullException(nameof(ownerControl));
            service = svc ?? throw new ArgumentNullException(nameof(svc));
            _persist = persist ?? (() => { });

            FullPath = item.NupkgPath;
            File = System.IO.Path.GetFileName(FullPath);
            Path = System.IO.Path.GetDirectoryName(FullPath) ?? "";

            Log = $"Started at {DateTime.Now}\r\n";

            // restore persisted timestamps/status (if you have these fields on the item)
            FileUpdated = item.LastFileWriteUtc?.ToLocalTime() ?? default;
            PluginUpdated = item.LastUploadUtc?.ToLocalTime() ?? default;
            Status = !string.IsNullOrWhiteSpace(item.LastStatus) ? item.LastStatus : "Initializing...";

            if (item.PackageId == Guid.Empty)
            {
                Status = "No PackageId set";
            }
            else if (string.IsNullOrWhiteSpace(item.NupkgPath) || !System.IO.File.Exists(item.NupkgPath))
            {
                Status = "File not found";
            }
            else
            {
                Status = "Watching";
                Watcher = new FileSystemWatcher
                {
                    Path = Path,
                    Filter = File,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = item.IsActive
                };
                Watcher.Changed += OnFileChanged;
            }

            ListItem = new ListViewItem { Tag = this };
            UpdateList();
        }

        #endregion Public Constructors

        #region Public Events

        public event EventHandler Changed;

        #endregion Public Events

        #region Public Properties

        public string File { get; }
        public DateTime FileUpdated { get; private set; }
        public string FullPath { get; }
        public ListViewItem ListItem { get; }
        public string Log { get; private set; } = "";
        public string Path { get; }
        public Guid PluginPackageId => item.PackageId;
        public string PluginPackageName => item.PackageName;
        public DateTime PluginUpdated { get; private set; }
        public string Status { get; private set; }
        public FileSystemWatcher Watcher { get; private set; }

        #endregion Public Properties

        #region Public Methods

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return; // already disposed
            }

            // Cancel + dispose debounce CTS safely
            var cts = Interlocked.Exchange(ref _debounceCts, null);
            if (cts != null)
            {
                try { cts.Cancel(); } catch { }
                try { cts.Dispose(); } catch { }
            }

            try { _uploadGate.Dispose(); } catch { }

            var watcher = Watcher;
            Watcher = null;
            if (watcher != null)
            {
                try { watcher.EnableRaisingEvents = false; } catch { }
                try { watcher.Changed -= OnFileChanged; } catch { }
                try { watcher.Dispose(); } catch { }
            }
        }

        #endregion Public Methods

        #region Private Methods

        private static string CreateBase64BlockIdWithoutPlusSlash()
        {
            while (true)
            {
                // 16 bytes => base64 like "xxxxxxxxxxxxxxxxxxxxxx=="
                var s = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                if (s.IndexOf('+') < 0 && s.IndexOf('/') < 0)
                {
                    return s;
                }
            }
        }

        private static byte[] ReadAllBytesShared(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var buffer = new byte[fs.Length];
                int read = 0;
                while (read < buffer.Length)
                {
                    int n = fs.Read(buffer, read, buffer.Length - read);
                    if (n <= 0)
                    {
                        break;
                    }

                    read += n;
                }
                return buffer;
            }
        }

        private static void UploadPluginPackageNupkg(IOrganizationService svc, Guid pluginPackageId, string filePath)
        {
            // Read with sharing (sometimes VS/MSBuild still has the file open briefly)
            var fileBytes = ReadAllBytesShared(filePath);
            var fileName = System.IO.Path.GetFileName(filePath);

            // Dataverse requires MimeType on CommitFileBlocksUpload
            // nupkg is basically a zip; octet-stream also works fine.
            const string mimeType = "application/zip";

            // 1) Initialize upload
            var initReq = new OrganizationRequest("InitializeFileBlocksUpload");
            initReq["Target"] = new EntityReference("pluginpackage", pluginPackageId);
            initReq["FileAttributeName"] = "package";
            initReq["FileName"] = fileName;

            var initResp = svc.Execute(initReq);
            var token = (string)initResp.Results["FileContinuationToken"];

            // 2) Upload blocks
            const int blockSize = 4 * 1024 * 1024; // 4MB
            var blockIds = new List<string>();

            int offset = 0;
            while (offset < fileBytes.Length)
            {
                int len = Math.Min(blockSize, fileBytes.Length - offset);
                var blockData = new byte[len];
                Buffer.BlockCopy(fileBytes, offset, blockData, 0, len);

                // MUST be valid base64. Also avoid '+' and '/' to prevent URI query issues.
                var blockId = CreateBase64BlockIdWithoutPlusSlash();
                blockIds.Add(blockId);

                var uploadReq = new OrganizationRequest("UploadBlock");
                uploadReq["FileContinuationToken"] = token;
                uploadReq["BlockId"] = blockId;
                uploadReq["BlockData"] = blockData;

                svc.Execute(uploadReq);

                offset += len;
            }

            // 3) Commit upload (FileName + MimeType are REQUIRED)
            var commitReq = new OrganizationRequest("CommitFileBlocksUpload");
            commitReq["FileContinuationToken"] = token;
            commitReq["BlockList"] = blockIds.ToArray();
            commitReq["FileName"] = fileName;
            commitReq["MimeType"] = mimeType;

            svc.Execute(commitReq);
        }

        private static async Task WaitForFileReadyAsync(string fullPath, CancellationToken token)
        {
            const int maxAttempts = 40; // ~10 seconds at 250ms
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    using (var stream = System.IO.File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        return;
                    }
                }
                catch (System.IO.FileNotFoundException) { }
                catch (UnauthorizedAccessException) { }
                catch (System.IO.IOException) { }

                await Task.Delay(250, token);
            }

            // final attempt to throw a meaningful exception if still not ready
            using (var stream = System.IO.File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }
        }

        private void AppendLog(string message)
        {
            Log += $"{DateTime.Now:HH:mm:ss.fff} {message}\r\n";
        }

        private async Task HandleFileChangedAsync(string fullPath, int myVersion, CancellationToken token)
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            try
            {
                if (Volatile.Read(ref _disposed) == 1)
                {
                    return;
                }

                if (myVersion != _changeVersion)
                {
                    return;
                }

                // Debounce multiple rapid change events
                await Task.Delay(DebounceMs, token);

                // Another change happened after this one -> ignore
                if (myVersion != _changeVersion)
                {
                    return;
                }

                await WaitForFileReadyAsync(fullPath, token);

                var lastWriteTime = System.IO.File.GetLastWriteTime(fullPath);
                if (lastWriteTime == FileUpdated)
                {
                    return;
                }

                bool shouldPersist = false;

                await _uploadGate.WaitAsync(token);
                try
                {
                    // Re-check again after acquiring lock
                    if (myVersion != _changeVersion)
                    {
                        return;
                    }

                    lastWriteTime = System.IO.File.GetLastWriteTime(fullPath);
                    if (lastWriteTime == FileUpdated)
                    {
                        return;
                    }

                    FileUpdated = lastWriteTime;
                    Status = "Updating package...";
                    AppendLog("File updated");
                    UpdateList();

                    // Your upload call (sync) - ok to call from here
                    UploadPluginPackageNupkg(service, item.PackageId, fullPath);

                    PluginUpdated = DateTime.Now;
                    Status = "Update ok";
                    AppendLog("Dataverse plugin package updated");
                    UpdateList();
                    item.LastFileWriteUtc = FileUpdated.ToUniversalTime();
                    item.LastUploadUtc = PluginUpdated.ToUniversalTime();
                    item.LastStatus = Status;
                    item.LastError = null;
                    shouldPersist = true;
                }
                finally
                {
                    _uploadGate.Release();
                }

                if (shouldPersist)
                {
                    Persist();
                }
            }
            catch (OperationCanceledException)
            {
                // expected when a newer change cancels the debounce
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
                AppendLog("ERROR: " + ex);
                UpdateList();
                item.LastStatus = Status;
                item.LastError = ex.Message;
                Persist();
            }
        }

        private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            var fullPath = e.FullPath;

            // Bump version so older scheduled runs bail out
            var myVersion = Interlocked.Increment(ref _changeVersion);

            // Swap CTS and cancel/dispose previous
            var prev = Interlocked.Exchange(ref _debounceCts, new CancellationTokenSource());
            if (prev != null)
            {
                try { prev.Cancel(); } catch { }
                try { prev.Dispose(); } catch { }
            }

            // If Dispose ran right after Exchange, _debounceCts could already be null
            var cts = _debounceCts;
            if (cts == null || Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            var token = cts.Token;

            // Fire-and-forget async handler
            _ = HandleFileChangedAsync(fullPath, myVersion, token);
        }

        private void Persist()
        {
            try
            {
                if (owner.IsDisposed || !owner.IsHandleCreated)
                {
                    return;
                }

                if (owner.InvokeRequired)
                {
                    owner.BeginInvoke(new Action(_persist));
                }
                else
                {
                    _persist();
                }
            }
            catch (Exception ex)
            {
                AppendLog("Persist failed: " + ex.Message);
                UpdateList();
            }
        }

        private void UpdateList()
        {
            MethodInvoker mi = delegate
            {
                while (ListItem.SubItems.Count < 5)
                {
                    ListItem.SubItems.Add(string.Empty);
                }

                var title = string.IsNullOrWhiteSpace(item.PackageName)
                    ? item.PackageId.ToString("D")
                    : item.PackageName;

                ListItem.Text = $"PKG: {title}";
                ListItem.SubItems[1].Text = Path;
                ListItem.SubItems[2].Text = FileUpdated.Ticks != 0 ? FileUpdated.ToString("HH:mm:ss.fff") : "";
                ListItem.SubItems[3].Text = PluginUpdated.Ticks != 0 ? PluginUpdated.ToString("HH:mm:ss.fff") : "";
                ListItem.SubItems[4].Text = Status;

                OnChanged();
            };

            if (owner.IsDisposed || !owner.IsHandleCreated)
            {
                return;
            }

            if (owner.InvokeRequired)
            {
                owner.BeginInvoke(mi);   // <-- change Invoke -> BeginInvoke
            }
            else
            {
                mi();
            }
        }

        #endregion Private Methods
    }
}