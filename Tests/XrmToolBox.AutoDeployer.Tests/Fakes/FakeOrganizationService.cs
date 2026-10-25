using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace XrmToolBox.AutoDeployer.Tests.Fakes
{
    // Answers RetrieveMultiple with a callback and records the queries; everything else is unsupported
    internal class FakeOrganizationService : IOrganizationService
    {
        #region Private Fields

        private readonly Func<QueryExpression, EntityCollection> retrieveMultiple;

        #endregion Private Fields

        #region Public Constructors

        public FakeOrganizationService(Func<QueryExpression, EntityCollection> retrieveMultiple)
        {
            this.retrieveMultiple = retrieveMultiple;
        }

        #endregion Public Constructors

        #region Public Properties

        public List<QueryExpression> Queries { get; } = new List<QueryExpression>();

        #endregion Public Properties

        #region Public Methods

        public static EntityCollection Records(string logicalName, params Guid[] ids)
        {
            var result = new EntityCollection { EntityName = logicalName };
            foreach (var id in ids)
            {
                result.Entities.Add(new Entity(logicalName, id));
            }
            return result;
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();

        public Guid Create(Entity entity) => throw new NotSupportedException();

        public void Delete(string entityName, Guid id) => throw new NotSupportedException();

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();

        public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var qe = (QueryExpression)query;
            Queries.Add(qe);
            return retrieveMultiple(qe);
        }

        public void Update(Entity entity) => throw new NotSupportedException();

        #endregion Public Methods
    }
}
