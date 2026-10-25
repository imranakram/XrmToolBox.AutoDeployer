using System;
using System.Collections.Generic;
using System.Linq;

namespace XrmToolBox.AutoDeployer
{
    internal enum WebResourceMergeResult
    {
        // Nothing configured yet: the incoming settings should be used as they are
        Replaced,

        // Same root folder: new mappings were added to the existing settings
        Merged,

        // Different root folder: nothing changed, the user has to choose
        RootDiffers
    }

    // Combines web resource settings from a project file with the ones of the connection
    internal static class WebResourceConfigMerger
    {
        #region Public Methods

        public static WebResourceMergeResult Merge(WebResourceWatchConfig existing, WebResourceWatchConfig incoming)
        {
            if (string.IsNullOrWhiteSpace(existing.RootPath) || existing.Mappings == null || existing.Mappings.Count == 0)
            {
                return WebResourceMergeResult.Replaced;
            }

            if (!SameFolder(existing.RootPath, incoming.RootPath))
            {
                return WebResourceMergeResult.RootDiffers;
            }

            foreach (var mapping in incoming.Mappings ?? new List<WebResourceMapping>())
            {
                if (!existing.Mappings.Any(m => string.Equals(m.RelativePath, mapping.RelativePath, StringComparison.OrdinalIgnoreCase)))
                {
                    existing.Mappings.Add(mapping);
                }
            }
            return WebResourceMergeResult.Merged;
        }

        #endregion Public Methods

        #region Private Methods

        private static bool SameFolder(string a, string b)
            => string.Equals(NormalizeFolder(a), NormalizeFolder(b), StringComparison.OrdinalIgnoreCase);

        private static string NormalizeFolder(string path)
            => (path ?? "").Replace('/', '\\').TrimEnd('\\');

        #endregion Private Methods
    }
}
