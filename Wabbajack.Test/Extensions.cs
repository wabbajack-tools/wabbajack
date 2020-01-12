using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

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
        
        public static async Task<T> RoundTripState<T>(this T state) where T : AbstractDownloadState
        {
            var ini = string.Join("\r\n", state.GetMetaIni()).LoadIniString();
            var round = (AbstractDownloadState) await DownloadDispatcher.ResolveArchive(ini);
            Assert.IsInstanceOfType(round, state.GetType());
            Assert.AreEqual(state.PrimaryKeyString, round.PrimaryKeyString);
            CollectionAssert.AreEqual(state.GetMetaIni(), round.GetMetaIni());
            return (T)round;
        }

    }
}
