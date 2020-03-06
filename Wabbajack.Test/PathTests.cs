using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class PathTests
    {
        [TestMethod]
        public async Task AbsolutePathTests()
        {
            var pathWithoutSlash = (AbsolutePath)"c:\\Windows\\system32";
            Assert.AreEqual(@"c:\windows\system32", (string)pathWithoutSlash, "Paths are normalized to lowercase");

            var pathWithBackSlash = (AbsolutePath)"c:\\Windows\\System32\\";
            Assert.AreEqual(@"c:\windows\system32", (string)pathWithBackSlash, "Slashes are trimmed");
            
            Assert.IsTrue(pathWithoutSlash.Exists, "Can check if a directory exists");

            var testFile = RelativePath.RandomFileName().RelativeToEntryPoint();
            Assert.IsFalse(testFile.Exists, "Random file shouldn't exist yet");

            await testFile.WriteAllTextAsync("this is a test");
            
            Assert.IsTrue(testFile.Exists, "Data was written to random file, it should now exist");
            
            Assert.AreEqual("this is a test", await testFile.ReadAllTextAsync(), "Read data is correct");
        }

        [TestMethod]
        public void PathNavigation()
        {
            var path = (AbsolutePath)@"c:\bar\baz\qux";
            Assert.AreEqual(@"c:\bar\baz", (string)path.Parent, "Can get a path parent");
            
            Assert.AreEqual(@"qux", (string)path.RelativeTo(path.Parent), "Can create a relative path from a parent path");
        }
        
    }
}
