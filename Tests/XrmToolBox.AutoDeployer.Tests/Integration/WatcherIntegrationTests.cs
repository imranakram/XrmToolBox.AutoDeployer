using System;
using System.Windows.Forms;
using NUnit.Framework;

namespace XrmToolBox.AutoDeployer.Tests.Integration
{
    // Deploys the test bench artifacts through the real watchers. Web resources aren't covered here:
    // WatchWebResourceFile runs its upload through the XrmToolBox host (WorkAsync on the UI thread),
    // so it's tested by hand with the test plan in Tests\TestBench\README.md.
    [TestFixture]
    [NonParallelizable]
    internal class WatcherIntegrationTests : DataverseFixture
    {
        #region Public Methods

        [Test]
        public void PluginAssemblyIsUpdatedWhenTheDllChanges()
        {
            var dll = CopyToTemp(AssemblyPath);
            var before = ModifiedOn("pluginassembly", AssemblyId);

            using (var owner = new Control())
            using (var watcher = new WatchPluginFile(dll, Service, owner))
            {
                Assert.That(watcher.PluginId, Is.EqualTo(AssemblyId), "the copied DLL should match the registered assembly");

                Rewrite(dll);
                WaitFor(() => watcher.Status == "Update ok" || watcher.Status.StartsWith("Error"), "the assembly upload");

                Assert.That(watcher.Status, Is.EqualTo("Update ok"), watcher.Log);
            }

            Assert.That(ModifiedOn("pluginassembly", AssemblyId), Is.GreaterThan(before));
        }

        [Test]
        public void PluginPackageIsFoundByNameWhenTheIdDoesNotExist()
        {
            var item = new PluginPackageWatchItem
            {
                PackageId = Guid.NewGuid(),
                PackageName = PackageName,
                NupkgPath = CopyToTemp(PackagePath),
                IsActive = true
            };
            var persisted = false;

            using (var owner = new Control())
            using (var watcher = new WatchPluginPackageFile(item, Service, owner, () => persisted = true))
            {
                Rewrite(item.NupkgPath);
                WaitFor(() => watcher.Status == "Update ok" || watcher.Status.StartsWith("Error"), "the package upload");

                Assert.That(watcher.Status, Is.EqualTo("Update ok"), watcher.Log);
                Assert.That(watcher.Log, Does.Contain("found by name"));
            }

            Assert.That(item.PackageId, Is.EqualTo(PackageId), "the watch item should now hold the real package id");
            Assert.That(item.LastError, Is.Null);
            // Persist goes through the owner's handle, which a test control doesn't have
            Assert.That(persisted, Is.False);
        }

        [Test]
        public void PluginPackageIsUpdatedWhenTheNupkgChanges()
        {
            var item = new PluginPackageWatchItem
            {
                PackageId = PackageId,
                PackageName = PackageName,
                NupkgPath = CopyToTemp(PackagePath),
                IsActive = true
            };
            var before = ModifiedOn("pluginpackage", PackageId);

            using (var owner = new Control())
            using (var watcher = new WatchPluginPackageFile(item, Service, owner, null))
            {
                Assert.That(watcher.Status, Is.EqualTo("Watching"));

                Rewrite(item.NupkgPath);
                WaitFor(() => watcher.Status == "Update ok" || watcher.Status.StartsWith("Error"), "the package upload");

                Assert.That(watcher.Status, Is.EqualTo("Update ok"), watcher.Log);
                Assert.That(watcher.Log, Does.Not.Contain("found by name"));
            }

            Assert.That(item.PackageId, Is.EqualTo(PackageId));
            Assert.That(ModifiedOn("pluginpackage", PackageId), Is.GreaterThan(before));
        }

        #endregion Public Methods
    }
}
