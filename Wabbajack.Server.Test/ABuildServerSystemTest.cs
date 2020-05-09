using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Wabbajack.Common;
using Wabbajack.Common.Http;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Server;
using Wabbajack.Server.DataLayer;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class BuildServerFixture : ADBTest, IDisposable
    {
        private IHost _host;
        private CancellationTokenSource _token;
        private Task _task;

        public readonly TempFolder _severTempFolder = TempFolder.Create().Result;
        private bool _disposed = false;
        public AbsolutePath ServerTempFolder => _severTempFolder.Dir;

        public AbsolutePath ServerPublicFolder => "public".RelativeTo(AbsolutePath.EntryPoint);

        public AbsolutePath ServerArchivesFolder => "archives".RelativeTo(AbsolutePath.EntryPoint);
        public AbsolutePath ServerUpdatesFolder => "updates".RelativeTo(AbsolutePath.EntryPoint);


        public BuildServerFixture()
        {
            var builder = Program.CreateHostBuilder(
                new[]
                {
                    $"WabbajackSettings:DownloadDir={"tmp".RelativeTo(AbsolutePath.EntryPoint)}",
                    $"WabbajackSettings:ArchiveDir={"archives".RelativeTo(AbsolutePath.EntryPoint)}",
                    $"WabbajackSettings:TempFolder={ServerTempFolder}",
                    $"WabbajackSettings:SQLConnection={PublicConnStr}",
                    $"WabbajackSettings:BunnyCDN_User=TEST",
                    $"WabbajackSettings:BunnyCDN_Password=TEST",
                    "WabbajackSettings:JobScheduler=false",
                    "WabbajackSettings:JobRunner=false",
                    "WabbajackSettings:RunBackEndJobs=false",
                    "WabbajackSettings:RunFrontEndJobs=false",
                    "WabbajackSettinss:DisableNexusForwarding=true"
                }, true);
            _host = builder.Build();
            _token = new CancellationTokenSource();
            _task = _host.RunAsync(_token.Token);
            Consts.WabbajackBuildServerUri = new Uri("http://localhost:8080");

            "ServerWhitelist.yaml".RelativeTo(ServerPublicFolder).WriteAllText(
                "GoogleIDs:\nAllowedPrefixes:\n    - http://localhost");
        }

        ~BuildServerFixture()
        {
            Dispose();
        }

        public T GetService<T>()
        {
            return (T)_host.Services.GetService(typeof(T));
        }



        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_token.IsCancellationRequested)
                _token.Cancel();

            try
            {
                _task.Wait();
            }
            catch (Exception)
            {
                // 
            }

            _severTempFolder.DisposeAsync().AsTask().Wait();
        }
    }

    /// <summary>
    /// Bit of a hack to get around that we don't want the system starting and stopping our
    /// HTTP server for each class its testing.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingletonAdaptor<T> where T : new()
    {
        private static T _singleton = default;
        private static object _lock = new object();
        public SingletonAdaptor()
        {
        }

        public T Deref()
        {
            lock (this)
            {
                if (_singleton == null)
                {
                    _singleton = new T();
                    if (_singleton is IAsyncLifetime d)
                    {
                        d.InitializeAsync().Wait();
                    }
                }

                return _singleton;
            }
        }

    }
    

    [Collection("ServerTests")]
    public class ABuildServerSystemTest : XunitContextBase, IClassFixture<SingletonAdaptor<BuildServerFixture>>
    {
        protected readonly Client _client;
        private readonly IDisposable _unsubMsgs;
        private readonly IDisposable _unsubErr;
        protected Client _authedClient;
        protected WorkQueue _queue;
        private Random _random;


        public ABuildServerSystemTest(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output)
        {
            Filters.Clear();
            _unsubMsgs = Utils.LogMessages.OfType<IInfo>().Subscribe(onNext: msg => XunitContext.WriteLine(msg.ShortDescription));
            _unsubErr = Utils.LogMessages.OfType<IUserIntervention>().Subscribe(msg =>
                XunitContext.WriteLine("ERROR: User intervention required: " + msg.ShortDescription));
            _client = new Client();
            _authedClient = new Client();
            Fixture = fixture.Deref();
            _authedClient.Headers.Add(("x-api-key", Fixture.APIKey));
            AuthorAPI.ApiKeyOverride = Fixture.APIKey;
            _queue = new WorkQueue();
            Queue = new WorkQueue();
            _random = new Random();

            Consts.ModlistSummaryURL = MakeURL("lists/status.json");
            Consts.ServerWhitelistURL = MakeURL("ServerWhitelist.yaml");
        }

        public WorkQueue Queue { get; set; }

        public BuildServerFixture Fixture { get; set; }

        protected string MakeURL(string path)
        {
            return "http://localhost:8080/" + path;
        }


        protected byte[] RandomData(long? size = null)
        {
            var arr = new byte[size ?? _random.Next(1024)];
            _random.NextBytes(arr);
            return arr;
        }


        public override void Dispose()
        {
            Queue.Dispose();
            base.Dispose();
            _unsubMsgs.Dispose();
            _unsubErr.Dispose();
            
        }
    }
}
