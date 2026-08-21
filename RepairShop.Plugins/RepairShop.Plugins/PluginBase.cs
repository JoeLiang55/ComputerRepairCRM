using System;
using Microsoft.Xrm.Sdk;

namespace RepairShop.Plugins
{
    /// <summary>
    /// Provides the common Dataverse service resolution, tracing, and exception boundary.
    /// Invocation-specific state must remain local because Dataverse can reuse plug-in instances.
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var tracingService =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (tracingService == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Dataverse tracing service is unavailable.");
            }

            var context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            if (context == null)
            {
                tracingService.Trace("Execution stopped: plug-in context is unavailable.");
                throw new InvalidPluginExecutionException(
                    "The Dataverse plug-in execution context is unavailable.");
            }

            string pluginName = GetType().Name;
            tracingService.Trace(
                "{0}: Start. CorrelationId={1}, OperationId={2}, Depth={3}.",
                pluginName,
                context.CorrelationId,
                context.OperationId,
                context.Depth);

            try
            {
                ExecutePlugin(serviceProvider, context, tracingService);
                tracingService.Trace("{0}: Completed successfully.", pluginName);
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracingService.Trace("{0}: Execution error: {1}", pluginName, ex);
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("{0}: Unexpected error: {1}", pluginName, ex);
                throw new InvalidPluginExecutionException(
                    string.Format(
                        "{0} Contact an administrator and provide correlation ID {1}.",
                        UnexpectedErrorMessage,
                        context.CorrelationId),
                    ex);
            }
        }

        protected abstract string UnexpectedErrorMessage { get; }

        protected abstract void ExecutePlugin(
            IServiceProvider serviceProvider,
            IPluginExecutionContext context,
            ITracingService tracingService);

        protected static bool TryGetTarget(
            IPluginExecutionContext context,
            out Entity target)
        {
            target = null;
            if (context.InputParameters == null || !context.InputParameters.Contains("Target"))
            {
                return false;
            }

            target = context.InputParameters["Target"] as Entity;
            return target != null;
        }
    }
}
