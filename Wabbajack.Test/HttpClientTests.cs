using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Wabbajack.Test
{
    [TestClass]
    public class HttpClientTests
    {
        [TestMethod]
        public async Task DoesntReuseHttpMessages()
        {
            var client = new Common.Http.Client();
            // If we reuse the HTTP message this will throw a invalid operation exception
            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () => await client.GetAsync("http://blerg.blaz.bloz.buz"));
        }
    }
}
