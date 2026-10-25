using System;
using System.Collections.Generic;

namespace XrmToolBox.AutoDeployer
{
    public enum WatchFileType
    {
        PluginAssembly,
        PluginPackage,
        WebResource
    }

    public class Project
    {
        #region Public Constructors

        public Project()
        { }

        #endregion Public Constructors

        #region Public Properties

        public List<WatchFile> WatchFiles { get; set; } = new List<WatchFile>();

        // Web resources are stored as their full settings (root folder + mappings),
        // since a single file path is not enough to recreate a web resource watch.
        public WebResourceWatchConfig WebResources { get; set; }

        #endregion Public Properties
    }

    public class WatchFile
    {
        #region Public Properties

        public Guid PackageId { get; set; }
        public string PackageName { get; set; }
        public string Path { get; set; }
        public WatchFileType Type { get; set; }

        #endregion Public Properties
    }
}
