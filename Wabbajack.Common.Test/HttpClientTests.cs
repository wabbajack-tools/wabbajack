using System.Threading.Tasks;
using Wabbajack.Common.Exceptions;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class HttpClientTests
    {
        [Fact]
        public async Task DoesntReuseHttpMessages()
        {
            var client = new Common.Http.Client();
            // If we reuse the HTTP message this will throw a invalid operation exception
            await Assert.ThrowsAsync<HttpException>(async () => await client.GetAsync("http://blerg.blaz.bloz.buz"));
        }
    }
}
