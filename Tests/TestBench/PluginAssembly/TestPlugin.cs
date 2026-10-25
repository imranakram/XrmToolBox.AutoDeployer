using System;
using Microsoft.Xrm.Sdk;

namespace AutoDeployTest.Plugins
{
    /// <summary>
    /// Does nothing but trace. It exists so there is a registered assembly to deploy over.
    /// Change BuildMarker and rebuild to give AutoDeployer a new DLL to pick up.
    /// </summary>
    public class TestPlugin : IPlugin
    {
        public const string BuildMarker = "initial";

        public void Execute(IServiceProvider serviceProvider)
        {
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            tracing?.Trace($"AutoDeployTest.Plugins.TestPlugin ({BuildMarker})");
        }
    }
}
