using System;
using System.IO;
using NUnit.Framework;

namespace XrmToolBox.AutoDeployer.Tests
{
    // Locations in Tests\TestBench and the connection string for the integration tests
    internal static class TestBench
    {
        #region Public Properties

        public static string Folder { get; } = FindFolder();

        #endregion Public Properties

        #region Public Methods

        public static string PathTo(string relativePath) => Path.Combine(Folder, relativePath);

        // AUTODEPLOYER_TEST_CONNECTION, else Tests\connection.local.txt (gitignored). Null when neither is set.
        public static string ConnectionString()
        {
            var cs = Environment.GetEnvironmentVariable("AUTODEPLOYER_TEST_CONNECTION");
            if (!string.IsNullOrWhiteSpace(cs))
            {
                return cs.Trim();
            }

            var file = Path.Combine(Path.GetDirectoryName(Folder), "connection.local.txt");
            return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
        }

        #endregion Public Methods

        #region Private Methods

        private static string FindFolder()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Tests", "TestBench");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Tests\\TestBench not found above " + TestContext.CurrentContext.TestDirectory);
        }

        #endregion Private Methods
    }
}
