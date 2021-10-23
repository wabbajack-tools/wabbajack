using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Wabbajack.App.Test;

public class Startup
{
    public void ConfigureServices(IServiceCollection service)
    {
        service.AddAppServices();
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
        MessageBus.Instance = new SimpleMessageBus();
    }
}

public class SimpleMessageBus : IMessageBus
{
    public List<object> Messages { get; } = new();

    public void Send<T>(T message)
    {
        Messages.Add(message);
    }
}