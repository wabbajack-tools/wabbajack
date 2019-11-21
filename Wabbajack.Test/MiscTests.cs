using Wabbajack.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils = Wabbajack.Common.Utils;

namespace Wabbajack.Test
{
    [TestClass]
    public class MiscTests
    {
        [TestMethod]
        public void TestDiskSpeed()
        {
            using (var queue = new WorkQueue())
            {
                var speed = Utils.TestDiskSpeed(queue, @".\");
            }
        } 
    }
}
