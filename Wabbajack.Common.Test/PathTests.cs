using Xunit;

namespace Wabbajack.Common.Test
{
    public class PathTests
    {
        [Fact]
        public void CanDeleteReadOnlyFile()
        {
            var tempFile = new TempFile();
            tempFile.Path.WriteAllText("Test");
            tempFile.Path.SetReadOnly(true);
            
            tempFile.Path.Delete();
        }
        
        [Fact]
        public void CanMoveReadOnlyFiles()
        {
            var tempFile = new TempFile();
            var tempFile2 = new TempFile();
            tempFile.Path.WriteAllText("Test");
            tempFile.Path.SetReadOnly(true);
            
            tempFile.Path.MoveTo(tempFile2.Path);
        }
    }
}
