using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class PathTests
    {
        [Fact]
        public async Task CanDeleteReadOnlyFile()
        {
            var tempFile = new TempFile();
            await tempFile.Path.WriteAllTextAsync("Test");
            tempFile.Path.SetReadOnly(true);
            
            await tempFile.Path.DeleteAsync();
        }
        
        [Fact]
        public async Task CanMoveReadOnlyFiles()
        {
            var tempFile = new TempFile();
            var tempFile2 = new TempFile();
            await tempFile.Path.WriteAllTextAsync("Test");
            tempFile.Path.SetReadOnly(true);
            
            await tempFile.Path.MoveToAsync(tempFile2.Path);
        }

        [Fact]
        public void CanGetTopParentOfPath()
        {
            var path = (RelativePath)"foo/bar";
            Assert.Equal((RelativePath)"foo", path.TopParent);
            
        }
        
        [Fact]
        public void CanGetTopParentOfSinglePath()
        {
            var path = (RelativePath)"foo";
            Assert.Equal((RelativePath)"foo", path.TopParent);
        }
    }
}
