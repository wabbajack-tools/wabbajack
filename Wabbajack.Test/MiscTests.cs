using Alphaleonis.Win32.Filesystem;
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

        [TestMethod]
        public void TestHash()
        {
            const string data = "Cheese for Everyone!";
            File.WriteAllText("test.data", data);
            Assert.AreEqual("eSIyd+KOG3s=", "test.data".FileHashCached(), "Hash is cached");
            Assert.IsTrue(Utils.TryGetHashCache("test.data", out var fileHash), "New caching method is invoked");
            Assert.AreEqual("eSIyd+KOG3s=", fileHash, "The correct hash value is cached");
            Assert.AreNotEqual("eSIyd+KOG3s=", File.ReadAllText("test.data" + Consts.HashFileExtension), "We don't store the hash in plaintext");
        }
    }
}
