using System.Linq;
using NUnit.Framework;

namespace XrmToolBox.AutoDeployer.Tests
{
    [TestFixture]
    internal class WebResourceConfigMergerTests
    {
        #region Public Methods

        [Test]
        public void DifferentRootLeavesExistingUntouched()
        {
            var existing = Config(@"C:\src\wr", "a.js");
            var incoming = Config(@"C:\other\wr", "b.js");

            var result = WebResourceConfigMerger.Merge(existing, incoming);

            Assert.That(result, Is.EqualTo(WebResourceMergeResult.RootDiffers));
            Assert.That(existing.Mappings.Select(m => m.RelativePath), Is.EqualTo(new[] { "a.js" }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(@"C:\src\wr")]
        public void NothingConfiguredMeansReplace(string existingRoot)
        {
            // A root without any mappings counts as not configured too
            var existing = new WebResourceWatchConfig { RootPath = existingRoot };
            var incoming = Config(@"C:\src\wr", "a.js");

            Assert.That(WebResourceConfigMerger.Merge(existing, incoming), Is.EqualTo(WebResourceMergeResult.Replaced));
        }

        [Test]
        public void NullMappingsMeansReplace()
        {
            var existing = new WebResourceWatchConfig { RootPath = @"C:\src\wr", Mappings = null };

            Assert.That(WebResourceConfigMerger.Merge(existing, Config(@"C:\src\wr", "a.js")), Is.EqualTo(WebResourceMergeResult.Replaced));
        }

        [TestCase(@"C:\src\wr")]
        [TestCase(@"c:\SRC\WR")]
        [TestCase(@"C:\src\wr\")]
        [TestCase(@"C:/src/wr/")]
        public void SameRootMergesWithoutDuplicates(string incomingRoot)
        {
            var existing = Config(@"C:\src\wr", "a.js", "b.js");
            var incoming = Config(incomingRoot, "B.JS", "c.js");

            var result = WebResourceConfigMerger.Merge(existing, incoming);

            Assert.That(result, Is.EqualTo(WebResourceMergeResult.Merged));
            Assert.That(existing.Mappings.Select(m => m.RelativePath), Is.EqualTo(new[] { "a.js", "b.js", "c.js" }));
        }

        #endregion Public Methods

        #region Private Methods

        private static WebResourceWatchConfig Config(string root, params string[] relativePaths)
        {
            var cfg = new WebResourceWatchConfig { RootPath = root, Prefix = "adt_/x" };
            foreach (var rel in relativePaths)
            {
                cfg.Mappings.Add(new WebResourceMapping { RelativePath = rel, CrmName = "adt_/x/" + rel, IsActive = true });
            }
            return cfg;
        }

        #endregion Private Methods
    }
}
