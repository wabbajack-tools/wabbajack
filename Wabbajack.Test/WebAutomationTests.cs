using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Test
{
    /// <summary>
    /// Summary description for WebAutomationTests
    /// </summary>
    [TestClass]
    public class WebAutomationTests
    {
        /* 
        [TestMethod]
        public async Task TestBasicNavigation()
        {
            using (var w = await Driver.Create())
            {
                await w.NavigateTo(new Uri("http://www.google.com"));
                Assert.AreEqual("www.google.com", (await w.GetLocation()).Host);
            }
        }

        [TestMethod]
        public async Task TestAttrSelection()
        {
            using (var w = await Driver.Create())
            {
                await w.NavigateTo(new Uri("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.tx"));
                Assert.IsTrue((await w.GetAttr("a.input", "href")).StartsWith("http://"));
            }
        }
        */
    }
}
