using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Wabbajack.Test
{
    [TestClass]
    public class VortexTests : AVortexCompilerTest
    {
        // TODO: figure out what games we want installed on the test server for this
        /*
        [TestMethod]
        public void TestVortexStackSerialization()
        {
            utils.AddMod("test");
            utils.Configure();

            var vortexCompiler = ConfigureAndRunCompiler();
            vortexCompiler.StagingFolder = "vortex_staging";
            var stack = vortexCompiler.MakeStack();

            var serialized = Serialization.Serialize(stack);
            var rounded = Serialization.Serialize(Serialization.Deserialize(serialized, vortexCompiler));

            Assert.AreEqual(serialized, rounded);
            Assert.IsNotNull(vortexCompiler.GetStack());
            
        }
        */
    }
}
