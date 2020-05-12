using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class MiscTests
    {
        [Fact]
        public void TestDiskSpeed()
        {
            using (var queue = new WorkQueue())
            {
                var speed = Utils.TestDiskSpeed(queue, AbsolutePath.EntryPoint);
            }
        }

        [Fact]
        public async Task TestHash()
        {
            var testFile = ((RelativePath)"text.data").RelativeToEntryPoint();
            const string data = "Cheese for Everyone!";
            await testFile.WriteAllTextAsync(data);
            File.WriteAllText("test.data", data);
            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await testFile.FileHashCachedAsync());
            Assert.True(Utils.TryGetHashCache(testFile, out var fileHash));
            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), fileHash);
        }
    }
}
