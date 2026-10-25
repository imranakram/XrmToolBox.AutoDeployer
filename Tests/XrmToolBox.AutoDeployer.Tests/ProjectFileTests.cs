using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace XrmToolBox.AutoDeployer.Tests
{
    [TestFixture]
    internal class ProjectFileTests
    {
        #region Private Fields

        private static readonly Guid PackageId = new Guid("7c2c3f3a-67d0-f111-a05b-7ced8d3ebe2f");
        private string tempFile;

        #endregion Private Fields

        #region Public Methods

        [Test]
        public void EnumNamesAreWrittenAsText()
        {
            // Changing these breaks every saved .adproj file
            var project = new Project();
            project.WatchFiles.Add(new WatchFile { Type = WatchFileType.PluginAssembly, Path = "a.dll" });
            project.WatchFiles.Add(new WatchFile { Type = WatchFileType.PluginPackage, Path = "b.nupkg" });
            project.WatchFiles.Add(new WatchFile { Type = WatchFileType.WebResource, Path = "c.js" });

            ProjectFile.Save(project, tempFile);
            var xml = File.ReadAllText(tempFile);

            Assert.That(xml, Does.Contain("<Type>PluginAssembly</Type>"));
            Assert.That(xml, Does.Contain("<Type>PluginPackage</Type>"));
            Assert.That(xml, Does.Contain("<Type>WebResource</Type>"));
        }

        [Test]
        public void LegacySampleHasWebResourcePathsButNoSettings()
        {
            var project = TestBench.LoadSampleProject("AutoDeployTest-Legacy.adproj");

            Assert.That(project.WebResources, Is.Null);
            Assert.That(project.WatchFiles.Select(w => w.Type),
                Is.EqualTo(new[] { WatchFileType.PluginAssembly, WatchFileType.WebResource }));
        }

        [Test]
        public void MainSampleLoads()
        {
            var project = TestBench.LoadSampleProject("AutoDeployTest.adproj");
            var prefix = TestBench.SamplePrefix;

            Assert.That(project.WatchFiles.Select(w => w.Type),
                Is.EqualTo(new[] { WatchFileType.PluginAssembly, WatchFileType.PluginPackage }));
            var package = project.WatchFiles.Single(w => w.Type == WatchFileType.PluginPackage);
            Assert.That(package.PackageId, Is.EqualTo(TestBench.SamplePackageId));
            Assert.That(package.PackageName, Is.EqualTo($"{prefix}_AutoDeployTest.Package"));
            Assert.That(project.WebResources.Prefix, Is.EqualTo($"{prefix}_/autodeploytest"));
            Assert.That(project.WebResources.Mappings.Select(m => m.CrmName), Is.EquivalentTo(new[]
            {
                $"{prefix}_/autodeploytest/scripts/test.js",
                $"{prefix}_/autodeploytest/styles/test.css",
                $"{prefix}_/autodeploytest/pages/test.html"
            }));
            Assert.That(project.WebResources.Patterns.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                Is.EqualTo(new[] { "*.js", "*.css", "*.html" }));
        }

        [Test]
        public void OtherRootSampleHasOnlyWebResources()
        {
            var project = TestBench.LoadSampleProject("AutoDeployTest-OtherRoot.adproj");

            Assert.That(project.WatchFiles, Is.Empty);
            Assert.That(project.WebResources.RootPath, Is.EqualTo(TestBench.PathTo(@"WebResources\scripts")));
            Assert.That(project.WebResources.Mappings, Has.Count.EqualTo(1));
        }

        [Test]
        public void SamplePathsPointIntoTheTestBench()
        {
            var project = TestBench.LoadSampleProject("AutoDeployTest.adproj");

            Assert.That(project.WatchFiles.Select(w => w.Path),
                Is.EqualTo(new[]
                {
                    TestBench.PathTo(@"PluginAssembly\bin\Debug\net462\AutoDeployTest.Plugins.dll"),
                    TestBench.PathTo(@"PluginPackage\bin\Debug\AutoDeployTest.Package.1.0.0.nupkg")
                }));
            Assert.That(project.WebResources.RootPath, Is.EqualTo(TestBench.PathTo("WebResources")));
        }

        [Test]
        public void SamplesWorkInAFolderWithXmlCharacters()
        {
            const string folder = @"C:\R&D <tools>\TestBench";

            var project = TestBench.LoadSampleProject("AutoDeployTest.adproj", folder);

            Assert.That(project.WebResources.RootPath, Is.EqualTo(folder + @"\WebResources"));
            Assert.That(project.WatchFiles[0].Path, Does.StartWith(folder + @"\PluginAssembly"));
        }

        // The templates must work for any clone, environment and publisher
        [TestCase("AutoDeployTest.adproj")]
        [TestCase("AutoDeployTest-Legacy.adproj")]
        [TestCase("AutoDeployTest-OtherRoot.adproj")]
        [TestCase("AutoDeployTest-WrongPackageId.adproj")]
        public void TemplatesUsePlaceholders(string fileName)
        {
            var text = File.ReadAllText(TestBench.PathTo(Path.Combine("Projects", fileName)));

            Assert.That(text, Does.Not.Match(@"[A-Za-z]:\\"), "absolute path");
            Assert.That(text, Does.Not.Match(@">[a-z0-9]+_[/A-Z]"), "concrete publisher prefix");
        }

        [Test]
        public void RoundTripKeepsEverything()
        {
            var project = new Project
            {
                WebResources = new WebResourceWatchConfig
                {
                    RootPath = @"C:\src\wr",
                    Prefix = "adt_/app",
                    Patterns = "*.js",
                    PublishEnabled = true,
                    DebounceMs = 900
                }
            };
            project.WebResources.Mappings.Add(new WebResourceMapping { CrmName = "adt_/app/a.js", RelativePath = "a.js", IsActive = true });
            project.WebResources.Mappings.Add(new WebResourceMapping { CrmName = "adt_/app/b.js", RelativePath = "b.js", IsActive = false });
            project.WatchFiles.Add(new WatchFile { Type = WatchFileType.PluginAssembly, Path = @"C:\src\My.Plugins.dll" });
            project.WatchFiles.Add(new WatchFile { Type = WatchFileType.PluginPackage, Path = @"C:\src\My.Package.1.0.0.nupkg", PackageId = PackageId, PackageName = "adt_My.Package" });

            ProjectFile.Save(project, tempFile);
            var loaded = ProjectFile.Load(tempFile);

            Assert.That(loaded.WatchFiles, Has.Count.EqualTo(2));
            Assert.That(loaded.WatchFiles[0].Type, Is.EqualTo(WatchFileType.PluginAssembly));
            Assert.That(loaded.WatchFiles[0].Path, Is.EqualTo(@"C:\src\My.Plugins.dll"));
            Assert.That(loaded.WatchFiles[1].Type, Is.EqualTo(WatchFileType.PluginPackage));
            Assert.That(loaded.WatchFiles[1].PackageId, Is.EqualTo(PackageId));
            Assert.That(loaded.WatchFiles[1].PackageName, Is.EqualTo("adt_My.Package"));

            var wr = loaded.WebResources;
            Assert.That(wr.RootPath, Is.EqualTo(@"C:\src\wr"));
            Assert.That(wr.Prefix, Is.EqualTo("adt_/app"));
            Assert.That(wr.Patterns, Is.EqualTo("*.js"));
            Assert.That(wr.PublishEnabled, Is.True);
            Assert.That(wr.DebounceMs, Is.EqualTo(900));
            Assert.That(wr.Mappings.Select(m => (m.CrmName, m.RelativePath, m.IsActive)), Is.EqualTo(new[]
            {
                ("adt_/app/a.js", "a.js", true),
                ("adt_/app/b.js", "b.js", false)
            }));
        }

        [SetUp]
        public void SetUp() => tempFile = Path.Combine(Path.GetTempPath(), $"adproj-{Guid.NewGuid():N}.adproj");

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void WrongPackageIdSampleKeepsTheName()
        {
            var project = TestBench.LoadSampleProject("AutoDeployTest-WrongPackageId.adproj");

            var package = project.WatchFiles.Single();
            Assert.That(package.Type, Is.EqualTo(WatchFileType.PluginPackage));
            Assert.That(package.PackageId, Is.EqualTo(new Guid("11111111-2222-3333-4444-555555555555")));
            Assert.That(package.PackageName, Is.EqualTo($"{TestBench.SamplePrefix}_AutoDeployTest.Package"));
        }

        #endregion Public Methods
    }
}
