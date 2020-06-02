using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class UtilsTests
    {
       
        [Fact]
        public void IsInPathTests()
        {
            Assert.True("c:\\foo\\bar.exe".IsInPath("c:\\foo"));
            Assert.False("c:\\foo\\bar.exe".IsInPath("c:\\fo"));
            Assert.True("c:\\Foo\\bar.exe".IsInPath("c:\\foo"));
            Assert.True("c:\\foo\\bar.exe".IsInPath("c:\\Foo"));
            Assert.True("c:\\foo\\bar.exe".IsInPath("c:\\fOo"));
            Assert.True("c:\\foo\\bar.exe".IsInPath("c:\\foo\\"));
            Assert.True("c:\\foo\\bar\\".IsInPath("c:\\foo\\"));
        }


        [Theory]
        [ClassData(typeof(PatchData))]
        public async Task DiffCreateAndApply(byte[] src, byte[] dest, DiffMethod method)
        {
            await using var ms = new MemoryStream();
            switch (method)
            {
                case DiffMethod.Default:
                    await Utils.CreatePatchCached(src, dest, ms);
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
            Assert.Equal(dest, resultStream.ToArray());
        }


        public enum DiffMethod
        {
            Default,
            BSDiff,
            OctoDiff
        }
        public class PatchData :  TheoryData<byte[], byte[], DiffMethod> 
        {
            public PatchData()
            {
                var maxSize = 64;
                Enumerable.Range(0, 10).Do(x =>

                {
                    Add(TestUtils.RandomData(maxSize: maxSize), TestUtils.RandomData(maxSize: maxSize),
                        (DiffMethod)TestUtils.RandomOne(DiffMethod.Default, DiffMethod.OctoDiff, DiffMethod.BSDiff));
                });
            }
        }
    }
}
