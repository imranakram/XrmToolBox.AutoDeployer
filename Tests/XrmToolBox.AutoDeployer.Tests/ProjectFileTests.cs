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
            var project = ProjectFile.Load(TestBench.PathTo(@"Projects\AutoDeployTest-Legacy.adproj"));

            Assert.That(project.WebResources, Is.Null);
            Assert.That(project.WatchFiles.Select(w => w.Type),
                Is.EqualTo(new[] { WatchFileType.PluginAssembly, WatchFileType.WebResource }));
        }

        [Test]
        public void MainSampleLoads()
        {
            var project = ProjectFile.Load(TestBench.PathTo(@"Projects\AutoDeployTest.adproj"));

            Assert.That(project.WatchFiles.Select(w => w.Type),
                Is.EqualTo(new[] { WatchFileType.PluginAssembly, WatchFileType.PluginPackage }));
            var package = project.WatchFiles.Single(w => w.Type == WatchFileType.PluginPackage);
            Assert.That(package.PackageId, Is.EqualTo(PackageId));
            Assert.That(package.PackageName, Is.EqualTo("biz_AutoDeployTest.Package"));
            Assert.That(project.WebResources.Prefix, Is.EqualTo("biz_/autodeploytest"));
            Assert.That(project.WebResources.Mappings.Select(m => m.CrmName), Is.EquivalentTo(new[]
            {
                "biz_/autodeploytest/scripts/test.js",
                "biz_/autodeploytest/styles/test.css",
                "biz_/autodeploytest/pages/test.html"
            }));
            Assert.That(project.WebResources.Patterns.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                Is.EqualTo(new[] { "*.js", "*.css", "*.html" }));
        }

        [Test]
        public void OtherRootSampleHasOnlyWebResources()
        {
            var project = ProjectFile.Load(TestBench.PathTo(@"Projects\AutoDeployTest-OtherRoot.adproj"));

            Assert.That(project.WatchFiles, Is.Empty);
            Assert.That(project.WebResources.RootPath, Does.EndWith(@"WebResources\scripts"));
            Assert.That(project.WebResources.Mappings, Has.Count.EqualTo(1));
        }

        [Test]
        public void RoundTripKeepsEverything()
        {
            var project = new Project
            {
                WebResources = new WebResourceWatchConfig
                {
                    RootPath = @"C:\src\wr",
                    Prefix = "biz_/app",
                    Patterns = "*.js",
                    PublishEnabled = true,
                    DebounceMs = 900
                }
            };
            project.WebResources.Mappings.Add(new WebResourceMapping { CrmName = "biz_/app/a.js", RelativePath = "a.js", IsActive = true });
            project.WebResources.Mappings.Add(new WebResourceMapping { CrmName = "biz_/app/b.js", RelativePath = "b.js", IsActive = false });
            project.WatchFiles.Add(new WatchFile { Type = WatchFileType.PluginAssembly, Path = @"C:\src\My.Plugins.dll" });
            project.WatchFiles.Add(new WatchFile { Type = WatchFileType.PluginPackage, Path = @"C:\src\My.Package.1.0.0.nupkg", PackageId = PackageId, PackageName = "biz_My.Package" });

            ProjectFile.Save(project, tempFile);
            var loaded = ProjectFile.Load(tempFile);

            Assert.That(loaded.WatchFiles, Has.Count.EqualTo(2));
            Assert.That(loaded.WatchFiles[0].Type, Is.EqualTo(WatchFileType.PluginAssembly));
            Assert.That(loaded.WatchFiles[0].Path, Is.EqualTo(@"C:\src\My.Plugins.dll"));
            Assert.That(loaded.WatchFiles[1].Type, Is.EqualTo(WatchFileType.PluginPackage));
            Assert.That(loaded.WatchFiles[1].PackageId, Is.EqualTo(PackageId));
            Assert.That(loaded.WatchFiles[1].PackageName, Is.EqualTo("biz_My.Package"));

            var wr = loaded.WebResources;
            Assert.That(wr.RootPath, Is.EqualTo(@"C:\src\wr"));
            Assert.That(wr.Prefix, Is.EqualTo("biz_/app"));
            Assert.That(wr.Patterns, Is.EqualTo("*.js"));
            Assert.That(wr.PublishEnabled, Is.True);
            Assert.That(wr.DebounceMs, Is.EqualTo(900));
            Assert.That(wr.Mappings.Select(m => (m.CrmName, m.RelativePath, m.IsActive)), Is.EqualTo(new[]
            {
                ("biz_/app/a.js", "a.js", true),
                ("biz_/app/b.js", "b.js", false)
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
            var project = ProjectFile.Load(TestBench.PathTo(@"Projects\AutoDeployTest-WrongPackageId.adproj"));

            var package = project.WatchFiles.Single();
            Assert.That(package.Type, Is.EqualTo(WatchFileType.PluginPackage));
            Assert.That(package.PackageId, Is.EqualTo(new Guid("11111111-2222-3333-4444-555555555555")));
            Assert.That(package.PackageName, Is.EqualTo("biz_AutoDeployTest.Package"));
        }

        #endregion Public Methods
    }
}
