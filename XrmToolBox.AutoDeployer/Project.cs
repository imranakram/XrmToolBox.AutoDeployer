using System;
using System.Collections.Generic;

namespace XrmToolBox.AutoDeployer
{
    public enum Type
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

        #endregion Public Properties
    }

    public class WatchFile
    {
        #region Public Properties

        public Guid PackageId { get; set; }
        public string PackageName { get; set; }
        public string Path { get; set; }
        public Type Type { get; set; }

        #endregion Public Properties
    }
}