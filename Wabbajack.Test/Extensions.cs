using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Wabbajack.Test
{
    public static class Extensions
    {
        public static void AssertIsFalse(this bool? condition)
        {
            Assert.IsFalse(condition ?? true, string.Empty, (object[])null);
        }
        public static void AssertIsTrue(this bool? condition)
        {
            Assert.IsTrue(condition ?? false, string.Empty, (object[])null);
        }

    }
}
