using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;
using XrmToolBox.AutoDeployer.Tests.Fakes;

namespace XrmToolBox.AutoDeployer.Tests
{
    [TestFixture]
    internal class PluginPackageLookupTests
    {
        #region Private Fields

        private static readonly Guid IdA = Guid.NewGuid();
        private static readonly Guid IdB = Guid.NewGuid();

        #endregion Private Fields

        #region Public Methods

        [Test]
        public void ExistsWhenFound()
        {
            var service = new FakeOrganizationService(q => FakeOrganizationService.Records("pluginpackage", IdA));

            Assert.That(PluginPackageLookup.Exists(service, IdA), Is.True);
            var condition = service.Queries.Single().Criteria.Conditions.Single();
            Assert.That(condition.AttributeName, Is.EqualTo("pluginpackageid"));
            Assert.That(condition.Values.Single(), Is.EqualTo(IdA));
        }

        [Test]
        public void ExistsWhenTheServiceFails()
        {
            // Unknown, so the caller keeps the original upload error instead of guessing
            var service = new FakeOrganizationService(q => throw new InvalidOperationException("offline"));

            Assert.That(PluginPackageLookup.Exists(service, IdA), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void FindByBlankNameDoesNotQuery(string name)
        {
            var service = new FakeOrganizationService(q => FakeOrganizationService.Records("pluginpackage", IdA));

            Assert.That(PluginPackageLookup.FindIdByName(service, name), Is.EqualTo(Guid.Empty));
            Assert.That(service.Queries, Is.Empty);
        }

        [Test]
        public void FindByNameIgnoresAmbiguousMatches()
        {
            var service = new FakeOrganizationService(q => FakeOrganizationService.Records("pluginpackage", IdA, IdB));

            Assert.That(PluginPackageLookup.FindIdByName(service, "biz_Pkg"), Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void FindByNameReturnsEmptyWhenMissing()
        {
            var service = new FakeOrganizationService(q => FakeOrganizationService.Records("pluginpackage"));

            Assert.That(PluginPackageLookup.FindIdByName(service, "biz_Pkg"), Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void FindByNameReturnsTheSingleMatch()
        {
            var service = new FakeOrganizationService(q => FakeOrganizationService.Records("pluginpackage", IdA));

            Assert.That(PluginPackageLookup.FindIdByName(service, "biz_Pkg"), Is.EqualTo(IdA));

            var query = service.Queries.Single();
            Assert.That(query.EntityName, Is.EqualTo("pluginpackage"));
            var condition = query.Criteria.Conditions.Single();
            Assert.That(condition.AttributeName, Is.EqualTo("name"));
            Assert.That(condition.Operator, Is.EqualTo(ConditionOperator.Equal));
            Assert.That(condition.Values.Single(), Is.EqualTo("biz_Pkg"));
        }

        [Test]
        public void NotExistsWhenNothingFound()
        {
            var service = new FakeOrganizationService(q => new EntityCollection());

            Assert.That(PluginPackageLookup.Exists(service, IdA), Is.False);
        }

        #endregion Public Methods
    }
}
