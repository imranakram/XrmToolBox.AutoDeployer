using NUnit.Framework;

namespace XrmToolBox.AutoDeployer.Tests
{
    [TestFixture]
    internal class WebResourceNamingTests
    {
        #region Public Methods

        [TestCase("adt_/app", @"scripts\main.js", ExpectedResult = "adt_/app/scripts/main.js")]
        [TestCase("adt_/app/", "scripts/main.js", ExpectedResult = "adt_/app/scripts/main.js")]
        [TestCase(@" adt_/app\ ", @"\main.js", ExpectedResult = "adt_/app/main.js")]
        [TestCase("adt_", "main.js", ExpectedResult = "adt_/main.js")]
        public string BuildCrmName(string prefix, string relativePath)
            => WebResourcesManagerDialog.BuildCrmName(prefix, relativePath);

        [TestCase(@"C:\src\wr", @"C:\src\wr\scripts\main.js", ExpectedResult = @"scripts\main.js")]
        [TestCase(@"C:\src\wr\", @"C:\src\wr\main.js", ExpectedResult = "main.js")]
        [TestCase(@"c:\SRC\wr", @"C:\src\WR\main.js", ExpectedResult = "main.js")]
        [TestCase(@"C:\src\wr", @"C:\elsewhere\main.js", ExpectedResult = "main.js")]
        [TestCase(@"C:\src\wr", @"C:\src\wr2\main.js", ExpectedResult = "main.js")]
        public string GetRelativePath(string root, string fullPath)
            => WebResourcesManagerDialog.GetRelativePath(root, fullPath);

        [TestCase(@"scripts\main.js", "*.js", ExpectedResult = true)]
        [TestCase(@"scripts\main.js", "*.css", ExpectedResult = false)]
        [TestCase(@"scripts\MAIN.JS", "*.js", ExpectedResult = true)]
        [TestCase("scripts/main.js", @"scripts\*.js", ExpectedResult = true)]
        [TestCase(@"styles\main.js", "scripts/*.js", ExpectedResult = false)]
        [TestCase("a1.js", "a?.js", ExpectedResult = true)]
        [TestCase("a12.js", "a?.js", ExpectedResult = false)]
        [TestCase("main.js", "main.js", ExpectedResult = true)]
        public bool WildcardMatch(string input, string pattern)
            => WebResourcesManagerDialog.WildcardMatch(input, pattern);

        #endregion Public Methods
    }
}
