using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;

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
            Get("/WABBAJACK_TEST_FILE.zip", _ => "Cheese for Everyone!");
        }
    }
}
