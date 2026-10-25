using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using NUnit.Framework;

namespace XrmToolBox.AutoDeployer.Tests
{
    // Locations in Tests\TestBench, the sample project templates, and the settings for the integration tests
    internal static class TestBench
    {
        #region Public Properties

        public static string Folder { get; } = FindFolder();

        // Stand in for the real values that Setup-Dataverse.ps1 fills into the sample projects
        public static Guid SamplePackageId { get; } = new Guid("0a1b2c3d-0000-4000-8000-000000000001");

        public static string SamplePrefix => "tst";

        // The same settings the test bench scripts read: Tests\testbench.local.json (gitignored),
        // each value overridable by an environment variable. Defaults to the test bench's own publisher.
        public static TestBenchSettings Settings { get; } = LoadSettings();

        #endregion Public Properties

        #region Public Methods

        // Loads a sample project template from Projects\, with its placeholders filled in
        // the way Setup-Dataverse.ps1 does it
        public static Project LoadSampleProject(string fileName)
        {
            var text = File.ReadAllText(PathTo(Path.Combine("Projects", fileName)))
                .Replace("$(TestBench)", Folder)
                .Replace("$(PackageId)", SamplePackageId.ToString())
                .Replace("$(Prefix)", SamplePrefix);
            var temp = Path.Combine(Path.GetTempPath(), $"sample-{Guid.NewGuid():N}.adproj");
            try
            {
                File.WriteAllText(temp, text);
                return ProjectFile.Load(temp);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        public static string PathTo(string relativePath) => Path.Combine(Folder, relativePath);

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

        private static TestBenchSettings LoadSettings()
        {
            var settings = new TestBenchSettings();
            var file = Path.Combine(Path.GetDirectoryName(Folder), "testbench.local.json");
            if (File.Exists(file))
            {
                using (var stream = File.OpenRead(file))
                {
                    settings = (TestBenchSettings)new DataContractJsonSerializer(typeof(TestBenchSettings)).ReadObject(stream);
                }
            }

            settings.Connection = Pick("AUTODEPLOYER_TEST_CONNECTION", settings.Connection, null);
            settings.Publisher = Pick("AUTODEPLOYER_TEST_PUBLISHER", settings.Publisher, "AutoDeployTest");
            settings.Prefix = Pick("AUTODEPLOYER_TEST_PREFIX", settings.Prefix, "adt");
            return settings;
        }

        private static string Pick(string variable, string fromFile, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = fromFile;
            }
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        #endregion Private Methods
    }

    [DataContract]
    internal class TestBenchSettings
    {
        #region Public Properties

        [DataMember(Name = "connection")]
        public string Connection { get; set; }

        [DataMember(Name = "prefix")]
        public string Prefix { get; set; }

        [DataMember(Name = "publisher")]
        public string Publisher { get; set; }

        #endregion Public Properties
    }
}
