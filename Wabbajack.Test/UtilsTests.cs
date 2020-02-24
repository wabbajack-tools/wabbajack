using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class UtilsTests
    {
       
        [TestMethod]
        public void IsInPathTests()
        {
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\foo"));
            Assert.IsFalse("c:\\foo\\bar.exe".IsInPath("c:\\fo"));
            Assert.IsTrue("c:\\Foo\\bar.exe".IsInPath("c:\\foo"));
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\Foo"));
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\fOo"));
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\foo\\"));
            Assert.IsTrue("c:\\foo\\bar\\".IsInPath("c:\\foo\\"));
        }


        [TestMethod]
        [DataTestMethod]
        [DynamicData(nameof(PatchData), DynamicDataSourceType.Method)]
        public async Task DiffCreateAndApply(byte[] src, byte[] dest, DiffMethod method)
        {
            await using var ms = new MemoryStream();
            switch (method)
            {
                case DiffMethod.Default:
                    await Utils.CreatePatch(src, dest, ms);
                    break;
                case DiffMethod.BSDiff:
                    BSDiff.Create(src, dest, ms);
                    break;
                case DiffMethod.OctoDiff:
                    OctoDiff.Create(src, dest, ms);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }

            ms.Position = 0;
            var patch = ms.ToArray();
            await using var resultStream = new MemoryStream();
            Utils.ApplyPatch(new MemoryStream(src), () => new MemoryStream(patch), resultStream);
            CollectionAssert.AreEqual(dest, resultStream.ToArray());
        }


        public enum DiffMethod
        {
            Default,
            BSDiff,
            OctoDiff
        }
        public static IEnumerable<object[]> PatchData()
        {
            var maxSize = 1024 * 1024 * 8;
            return Enumerable.Range(0, 10).Select(x => new[] {TestUtils.RandomData(maxSize:maxSize), TestUtils.RandomData(maxSize:maxSize), TestUtils.RandomeOne(DiffMethod.Default, DiffMethod.OctoDiff, DiffMethod.BSDiff)});
        }
    }
}
