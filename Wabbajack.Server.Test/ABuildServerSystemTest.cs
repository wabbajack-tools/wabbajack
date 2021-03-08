using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Server;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
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


        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            ServerArchivesFolder.DeleteDirectory().Wait();
            ServerArchivesFolder.CreateDirectory();
            
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
            Consts.TestMode = true;

            await "ServerWhitelist.yaml".RelativeTo(ServerPublicFolder).WriteAllTextAsync(
                "GoogleIDs:\nAllowedPrefixes:\n    - http://localhost");

            var sql = GetService<SqlService>();
            await sql.IngestMetric(new Metric
            {
                Action = "start",
                Subject = "tests",
                Timestamp = DateTime.UtcNow,
                MetricsKey = await Metrics.GetMetricsKey()
            });

        }

        ~BuildServerFixture()
        {
            Dispose();
        }

        public T GetService<T>()
        {
            var result = (T)_host.Services.GetService(typeof(T));

            if (result == null)
                throw new Exception($"Service {typeof(T)} not found in configuration");
            return result;
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
        protected readonly Wabbajack.Lib.Http.Client _client;
        private readonly IDisposable _unsubMsgs;
        private readonly IDisposable _unsubErr;
        protected Wabbajack.Lib.Http.Client _authedClient;
        protected WorkQueue _queue;
        protected Random Random;


        public ABuildServerSystemTest(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output)
        {
            Filters.Clear();
            _unsubMsgs = Utils.LogMessages.OfType<IInfo>().Subscribe(onNext: msg => XunitContext.WriteLine(msg.ShortDescription));
            _unsubErr = Utils.LogMessages.OfType<IUserIntervention>().Subscribe(msg =>
                XunitContext.WriteLine("ERROR: User intervention required: " + msg.ShortDescription));
            _client = new Wabbajack.Lib.Http.Client();
            _authedClient = new Wabbajack.Lib.Http.Client();
            Fixture = fixture.Deref();
            var cache = Fixture.GetService<MetricsKeyCache>();
            cache.AddKey(Metrics.GetMetricsKey().Result);
            _authedClient.Headers.Add(("x-api-key", Fixture.APIKey));
            AuthorAPI.ApiKeyOverride = Fixture.APIKey;
            _queue = new WorkQueue();
            Queue = new WorkQueue();
            Random = new Random();

            Consts.ModlistSummaryURL = MakeURL("lists/status.json");
            Consts.ServerWhitelistURL = MakeURL("ServerWhitelist.yaml");
            Consts.UnlistedModlistMetadataURL = MakeURL("lists/none.json");

        }

        public WorkQueue Queue { get; set; }

        public BuildServerFixture Fixture { get; set; }

        protected string MakeURL(string path)
        {
            return "http://localhost:8080/" + path;
        }


        protected byte[] RandomData(long? size = null)
        {
            var arr = new byte[size ?? Random.Next(1024)];
            Random.NextBytes(arr);
            return arr;
        }


        public override void Dispose()
        {
            Queue.Dispose();
            base.Dispose();
            _unsubMsgs.Dispose();
            _unsubErr.Dispose();
            
        }
        
        protected async Task<Uri> MakeModList(string modFileName)
        {
            var archive_data = Encoding.UTF8.GetBytes("Cheese for Everyone!");
            var test_archive_path = modFileName.RelativeTo(Fixture.ServerPublicFolder);
            await test_archive_path.WriteAllBytesAsync(archive_data);



            ModListData = new ModList();
            ModListData.Archives.Add(
                new Archive(new HTTPDownloader.State(MakeURL(modFileName)))
                {
                    Hash = await test_archive_path.FileHashAsync() ?? Hash.Empty,
                    Name = "test_archive",
                    Size = test_archive_path.Size,
                });
            
            var modListPath = "test_modlist.wabbajack".RelativeTo(Fixture.ServerPublicFolder);

            await using (var fs = await modListPath.Create())
            {
                using var za = new ZipArchive(fs, ZipArchiveMode.Create);
                var entry = za.CreateEntry("modlist");
                await using var es = entry.Open();
                ModListData.ToJson(es);
            }

            ModListMetaData = new List<ModlistMetadata>
            {
                new ModlistMetadata
                {
                    Official = false,
                    Author = "Test Suite",
                    Description = "A test",
                    DownloadMetadata = new DownloadMetadata
                    {
                        Hash = await modListPath.FileHashAsync() ?? Hash.Empty, 
                        Size = modListPath.Size
                    },
                    Links = new ModlistMetadata.LinksObject
                    {
                        MachineURL = "test_list",
                        Download = MakeURL("test_modlist.wabbajack")
                    }
                },
                new ModlistMetadata
                {
                    Official = true,
                    Author = "Test Suite",
                    Description = "A list with a broken hash",
                    DownloadMetadata = new DownloadMetadata()
                    {
                        Hash = Hash.FromLong(42),
                        Size = 42
                    },
                    Links = new ModlistMetadata.LinksObject
                    {
                        MachineURL = "broken_list",
                        Download = MakeURL("test_modlist.wabbajack")
                    }
                }
            };

            var metadataPath = "test_mod_list_metadata.json".RelativeTo(Fixture.ServerPublicFolder);

            await ModListMetaData.ToJsonAsync(metadataPath);
            
            return new Uri(MakeURL("test_mod_list_metadata.json"));
        }

        public ModList ModListData { get; set; }

        public List<ModlistMetadata> ModListMetaData { get; set; }
    }
}
