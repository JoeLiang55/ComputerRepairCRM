using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Moq;

namespace RepairShop.Plugins.Tests
{
    internal sealed class TestServiceProvider : IServiceProvider
    {
        private readonly IDictionary<Type, object> services = new Dictionary<Type, object>();

        internal TestServiceProvider Add<T>(T service)
        {
            services[typeof(T)] = service;
            return this;
        }

        public object GetService(Type serviceType)
        {
            object service;
            return services.TryGetValue(serviceType, out service) ? service : null;
        }
    }

    internal sealed class TestTracingService : ITracingService
    {
        internal IList<string> Messages { get; } = new List<string>();

        public void Trace(string format, params object[] args)
        {
            Messages.Add(args == null || args.Length == 0
                ? format
                : string.Format(format, args));
        }
    }

    internal static class PluginTestContext
    {
        internal static Mock<IPluginExecutionContext> Create(
            string primaryEntityName,
            int stage,
            Guid primaryEntityId,
            object target,
            Entity preImage = null,
            Entity postImage = null,
            string messageName = "Update")
        {
            var inputParameters = new ParameterCollection
            {
                { "Target", target }
            };
            var preImages = new EntityImageCollection();
            if (preImage != null)
            {
                preImages.Add("PreImage", preImage);
            }
            var postImages = new EntityImageCollection();
            if (postImage != null)
            {
                postImages.Add("PostImage", postImage);
            }

            var context = new Mock<IPluginExecutionContext>();
            context.SetupGet(item => item.MessageName).Returns(messageName);
            context.SetupGet(item => item.PrimaryEntityName).Returns(primaryEntityName);
            context.SetupGet(item => item.PrimaryEntityId).Returns(primaryEntityId);
            context.SetupGet(item => item.Stage).Returns(stage);
            context.SetupGet(item => item.Depth).Returns(1);
            context.SetupGet(item => item.CorrelationId).Returns(Guid.NewGuid());
            context.SetupGet(item => item.OperationId).Returns(Guid.NewGuid());
            context.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            context.SetupGet(item => item.InputParameters).Returns(inputParameters);
            context.SetupGet(item => item.PreEntityImages).Returns(preImages);
            context.SetupGet(item => item.PostEntityImages).Returns(postImages);
            return context;
        }
    }
}
