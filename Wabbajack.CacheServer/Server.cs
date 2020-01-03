using System;
using Alphaleonis.Win32.Filesystem;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Configuration;
using Nancy.Hosting.Self;
using Nancy.TinyIoc;
using Wabbajack.CacheServer.DTOs;
using Wabbajack.CacheServer.ServerConfig;
using Wabbajack.Common;

namespace Wabbajack.CacheServer
{
    public class Server : IDisposable
    {
        private NancyHost _server;
        private HostConfiguration _config;
        public static BuildServerConfig Config;

        static Server()
        {
            SerializerSettings.Init();
        }


        public Server(string address)
        {
            Address = address;
            _config = new HostConfiguration {MaximumConnectionCount = 200, RewriteLocalhost = true};
            //_config.UrlReservations.CreateAutomatically = true;
            _server = new NancyHost(_config, new Uri(address));

            Config = File.ReadAllText("config.yaml").FromYaml<BuildServerConfig>();
        }

        public string Address { get; }

        public void Start()
        {
            _server.Start();
        }

        public void Dispose()
        {
            _server?.Dispose();
        }
    }

    public class CachingBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            pipelines.AfterRequest.AddItemToEndOfPipeline(ctx =>
            {
                ctx.Response.WithHeader("Access-Control-Allow-Origin", "*")
                    .WithHeader("Access-Control-Allow-Methods", "POST, GET")
                    .WithHeader("Access-Control-Allow-Headers", "Accept, Origin, Content-type")
                    .WithHeader("Cache-Control","no-store");
            });
        }

        public override void Configure(INancyEnvironment environment)
        {
            environment.Tracing(
                enabled: true,
                displayErrorTraces: true);
        }

        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            container.Register<Heartbeat>();
            container.Register<JobQueueEndpoints>();
            container.Register<ListValidationService>();
            container.Register<Metrics>();
            container.Register<NexusCacheModule>();
            container.Register<TestingEndpoints>();
        }
    }
}
