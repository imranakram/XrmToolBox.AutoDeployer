using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using NUnit.Framework;

namespace XrmToolBox.AutoDeployer.Tests.Integration
{
    // Connects to the test environment and finds the components that Tests\TestBench\Scripts\Setup-Dataverse.ps1
    // registers. Everything is skipped (not failed) when there's no connection string or nothing is set up,
    // so these tests only run where someone has prepared an environment for them.
    [Category("Integration")]
    internal abstract class DataverseFixture
    {
        #region Protected Fields

        protected static readonly string PackageName = $"{TestBench.Settings.Prefix}_AutoDeployTest.Package";

        #endregion Protected Fields

        #region Private Fields

        private CrmServiceClient client;

        #endregion Private Fields

        #region Protected Properties

        protected string AssemblyPath => TestBench.PathTo(@"PluginAssembly\bin\Debug\net462\AutoDeployTest.Plugins.dll");

        protected Guid AssemblyId { get; private set; }

        protected string PackagePath =>
            Directory.Exists(TestBench.PathTo(@"PluginPackage\bin\Debug"))
                ? Directory.GetFiles(TestBench.PathTo(@"PluginPackage\bin\Debug"), "*.nupkg").FirstOrDefault()
                : null;

        protected Guid PackageId { get; private set; }

        protected IOrganizationService Service => client;

        protected string TempFolder { get; private set; }

        #endregion Protected Properties

        #region Public Methods

        [OneTimeSetUp]
        public void Connect()
        {
            var cs = TestBench.Settings.Connection;
            if (string.IsNullOrWhiteSpace(cs))
            {
                Assert.Ignore("No connection string: set AUTODEPLOYER_TEST_CONNECTION or create Tests\\testbench.local.json");
            }
            if (!File.Exists(AssemblyPath) || PackagePath == null)
            {
                Assert.Ignore("Build the test bench first: dotnet build Tests\\TestBench\\PluginAssembly and Tests\\TestBench\\PluginPackage");
            }

            client = new CrmServiceClient(cs);
            if (!client.IsReady)
            {
                Assert.Fail("Unable to connect: " + client.LastCrmError);
            }

            AssemblyId = FindId("pluginassembly", "name", "AutoDeployTest.Plugins");
            PackageId = FindId("pluginpackage", "name", PackageName);
            if (AssemblyId == Guid.Empty || PackageId == Guid.Empty)
            {
                Assert.Ignore($"Test components with prefix '{TestBench.Settings.Prefix}' not found: run Tests\\TestBench\\Scripts\\Setup-Dataverse.ps1 with the same settings");
            }

            TempFolder = Path.Combine(Path.GetTempPath(), "AutoDeployerTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempFolder);
        }

        [OneTimeTearDown]
        public void Disconnect()
        {
            client?.Dispose();
            if (TempFolder != null && Directory.Exists(TempFolder))
            {
                try { Directory.Delete(TempFolder, true); } catch { }
            }
        }

        #endregion Public Methods

        #region Protected Methods

        // Copies a test artifact to the temp folder, so the watcher can be pointed at a file we can rewrite
        protected string CopyToTemp(string source)
        {
            var target = Path.Combine(TempFolder, Guid.NewGuid().ToString("N"), Path.GetFileName(source));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target);
            return target;
        }

        protected DateTime ModifiedOn(string entityName, Guid id)
            => Service.Retrieve(entityName, id, new ColumnSet("modifiedon")).GetAttributeValue<DateTime>("modifiedon");

        // Writes the file again with a new timestamp, which is what a rebuild looks like to the watchers
        protected static void Rewrite(string path)
        {
            var bytes = File.ReadAllBytes(path);
            File.WriteAllBytes(path, bytes);
            File.SetLastWriteTime(path, DateTime.Now);
        }

        protected static void WaitFor(Func<bool> condition, string description, int timeoutSeconds = 90)
        {
            var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < until)
            {
                if (condition())
                {
                    return;
                }
                Thread.Sleep(500);
            }
            Assert.Fail($"Timed out after {timeoutSeconds}s waiting for {description}");
        }

        #endregion Protected Methods

        #region Private Methods

        private Guid FindId(string entityName, string attribute, string value)
        {
            var qe = new QueryExpression(entityName) { ColumnSet = new ColumnSet(false), TopCount = 1 };
            qe.Criteria.AddCondition(attribute, ConditionOperator.Equal, value);
            return Service.RetrieveMultiple(qe).Entities.FirstOrDefault()?.Id ?? Guid.Empty;
        }

        #endregion Private Methods
    }
}
