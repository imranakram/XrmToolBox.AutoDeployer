using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace XrmToolBox.AutoDeployer
{
    // Finding plugin packages in Dataverse, used when a watched package id doesn't exist in the
    // connected environment.
    internal static class PluginPackageLookup
    {
        #region Public Methods

        public static bool Exists(IOrganizationService service, Guid packageId)
        {
            try
            {
                var qe = new QueryExpression("pluginpackage")
                {
                    ColumnSet = new ColumnSet(false),
                    TopCount = 1
                };
                qe.Criteria.AddCondition("pluginpackageid", ConditionOperator.Equal, packageId);
                return service.RetrieveMultiple(qe).Entities.Count > 0;
            }
            catch
            {
                // Can't tell, so let the original upload error surface
                return true;
            }
        }

        public static Guid FindIdByName(IOrganizationService service, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Guid.Empty;
            }

            var qe = new QueryExpression("pluginpackage")
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 2
            };
            qe.Criteria.AddCondition("name", ConditionOperator.Equal, name);

            // Only trust an unambiguous match
            var found = service.RetrieveMultiple(qe).Entities;
            return found.Count == 1 ? found[0].Id : Guid.Empty;
        }

        #endregion Public Methods
    }
}
