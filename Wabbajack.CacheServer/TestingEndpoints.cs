using System.IO;
using System.Text;

namespace Wabbajack.CacheServer
{
    /// <summary>
    /// These endpoints are used by the testing service to verify that manual and direct
    /// downloading works as expected.
    /// </summary>
    public class TestingEndpoints : NancyModule
    {
        public TestingEndpoints() : base("/")
        {
            Get("/WABBAJACK_TEST_FILE.txt", _ => "Cheese for Everyone!");
            Get("/WABBAJACK_TEST_FILE.zip", _ =>
            {
                var response = new StreamResponse(() => new MemoryStream(Encoding.UTF8.GetBytes("Cheese for Everyone!")), "application/zip");
                return response.AsAttachment("WABBAJACK_TEST_FILE.zip");
            });
        }
    }
}
