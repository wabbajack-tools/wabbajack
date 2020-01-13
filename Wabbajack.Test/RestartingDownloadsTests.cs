using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Test
{
    public class CrappyRandomServer : SimpleHTTPServer
    {
        private Random _random;
        private readonly byte[] _data;

        public byte[] Data => _data;

        public CrappyRandomServer() : base("")
        {
            _random = new Random();
            _data = new byte[_random.Next(1024 * 1024, 1024 * 2048)];
            _random.NextBytes(_data);
        }

        protected override void Process(HttpListenerContext context)
        {
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength64 = _data.Length;
            context.Response.AddHeader("Accept-Ranges", "bytes");
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            var range = context.Request.Headers.Get("Range");
            int start = 0;
            if (range != null)
            {
                var match = new Regex("(?<=bytes=)[0-9]+(?=\\-)").Match(range);
                if (match != null)
                {
                    start = int.Parse(match.ToString());
                }
            }
            var end = Math.Min(start + _random.Next(1024 * 32, 1024 * 64), _data.Length);
            context.Response.OutputStream.Write(_data, start, end - start);
            context.Response.OutputStream.Flush();
            Thread.Sleep(500);
            context.Response.Abort();
        }
    }

    [TestClass]
    public class RestartingDownloadsTests
    {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Setup()
        {
            Utils.LogMessages.OfType<IInfo>().Subscribe(onNext: msg => TestContext.WriteLine(msg.ShortDescription));
            Utils.LogMessages.OfType<IUserIntervention>().Subscribe(msg =>
                TestContext.WriteLine("ERROR: User intervention required: " + msg.ShortDescription));
        }

        [TestMethod]
        public async Task DownloadResume()
        {
            using (var server = new CrappyRandomServer())
            {
                var downloader = DownloadDispatcher.GetInstance<HTTPDownloader>();
                var state = new HTTPDownloader.State {Url = $"http://localhost:{server.Port}/foo"};
                
                await state.Download("test.resume_file");

                CollectionAssert.AreEqual(server.Data, File.ReadAllBytes("test.resume_file"));

                if (File.Exists("test.resume_file"))
                    File.Delete("test.resume_file");
            }
        }
    }




}
