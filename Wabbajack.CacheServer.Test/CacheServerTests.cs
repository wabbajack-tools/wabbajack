using System;
using System.Net.Configuration;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.CacheServer.Test
{
    [TestClass]
    public class CacheServerTests
    {
        private Server _server;
        private HttpClient _client;
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Setup()
        {
            Utils.LogMessages.Subscribe(msg => TestContext.WriteLine(msg));
            _server = new Server("http://localhost:42420");
            _server.Start();
            _client = new NexusApiClient().HttpClient;
            _client.BaseAddress = new Uri(_server.Address);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _server.Dispose();
        }

        [TestMethod]
        public void TestAPIs()
        {
            _client.GetStringSync("/v1/games/skyrim/mods/70260.json");
            _client.GetStringSync("/v1/games/fallout4/mods/38590/files/156741.json");
            _client.GetStringAsync(
                "/nexus_cache_dir/68747470733a2f2f6170692e6e657875736d6f64732e636f6d2f76312f67616d65732f6f626c6976696f6e2f6d6f64732f383530302f66696c65732f363939332e6a736f6e.json");
        }
    }
}
