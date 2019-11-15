using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib.CompilationSteps;

namespace Wabbajack.Test
{
    [TestClass]
    public class VortexTests : AVortexCompilerTest
    {
        [TestMethod]
        public void TestVortexStackSerialization()
        {
            utils.AddMod("test");
            utils.Configure();

            var vortexCompiler = ConfigureAndRunCompiler();
            var stack = vortexCompiler.MakeStack();

            var serialized = Serialization.Serialize(stack);
            var rounded = Serialization.Serialize(Serialization.Deserialize(serialized, vortexCompiler));

            Assert.AreEqual(serialized, rounded);
            Assert.IsNotNull(vortexCompiler.GetStack());
        }
    }
}
