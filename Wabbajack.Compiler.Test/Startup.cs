using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Steam.UserInterventions;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;
using Configuration = Wabbajack.Services.OSIntegrated.Configuration;

namespace Wabbajack.Compiler.Test;

public class Startup
{
    public void ConfigureServices(IServiceCollection service)
    {
        service.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });

        service.AddScoped<ModListHarness>();
        service.AddSingleton<IUserInterventionHandler, UserInterventionHandler>();
    }

    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, delegate { return true; }));
    }
    
    public class UserInterventionHandler : IUserInterventionHandler
    {
        public void Raise(IUserIntervention intervention)
        {
            if (intervention is GetAuthCode gac)
            {
                switch (gac.Type)
                {
                    case GetAuthCode.AuthType.EmailCode:
                        Console.WriteLine("Please enter the Steam code that was just emailed to you");
                        break;
                    case GetAuthCode.AuthType.TwoFactorAuth:
                        Console.WriteLine("Please enter your 2FA code for Steam");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                gac.Finish(Console.ReadLine()!.Trim());
            }
        }
    }
}