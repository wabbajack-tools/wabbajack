using System;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Wabbajack.Common;
using Wabbajack.Common.Http;
using Wabbajack.Common.StatusFeed;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class BuildServerFixture : ADBTest, IDisposable
    {
        private IHost _host;
        private CancellationTokenSource _token;
        private Task _task;

        public readonly TempFolder _severTempFolder = new TempFolder();
        public AbsolutePath ServerTempFolder => _severTempFolder.Dir;


        public BuildServerFixture()
        {
            var builder = Program.CreateHostBuilder(
                new[]
                {
                    $"WabbajackSettings:DownloadDir={"tmp".RelativeTo(AbsolutePath.EntryPoint)}",
                    $"WabbajackSettings:ArchiveDir={"archives".RelativeTo(AbsolutePath.EntryPoint)}",
                    $"WabbajackSettings:TempDir={ServerTempFolder}",
                    $"WabbajackSettings:SQLConnection={PublicConnStr}",
                }, true);
            _host = builder.Build();
            _token = new CancellationTokenSource();
            _task = _host.RunAsync(_token.Token);
        }

        public void Dispose()
        {
            if (!_token.IsCancellationRequested)
                _token.Cancel();
            _task.Wait();
            _severTempFolder.DisposeAsync().AsTask().Wait();
        }
    }
    
    public class ABuildServerSystemTest : XunitContextBase, IClassFixture<BuildServerFixture>
    {
        protected readonly Client _client;
        private readonly IDisposable _unsubMsgs;
        private readonly IDisposable _unsubErr;
        protected Client _authedClient;


        public ABuildServerSystemTest(ITestOutputHelper output, BuildServerFixture fixture) : base(output)
        {
            _unsubMsgs = Utils.LogMessages.OfType<IInfo>().Subscribe(onNext: msg => XunitContext.WriteLine(msg.ShortDescription));
            _unsubErr = Utils.LogMessages.OfType<IUserIntervention>().Subscribe(msg =>
                XunitContext.WriteLine("ERROR: User intervention required: " + msg.ShortDescription));
            _client = new Client();
            _authedClient = new Client();
            _authedClient.Headers.Add(("x-api-key", fixture.APIKey));
            Fixture = fixture;
        }

        public BuildServerFixture Fixture { get; set; }

        protected string MakeURL(string path)
        {
            return "http://localhost:8080/" + path;
        }

        public override void Dispose()
        {

            base.Dispose();
            _unsubMsgs.Dispose();
            _unsubErr.Dispose();
        }
    }
}
