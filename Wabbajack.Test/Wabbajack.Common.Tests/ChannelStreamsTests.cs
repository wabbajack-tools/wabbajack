using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test.Wabbajack.Common.Tests
{
    [TestClass]
    public class ChannelStreamsTests
    {
        [TestMethod]
        public void ToAndFromChannel()
        {
            var src = Enumerable.Range(0, 10).ToList();
            var result = src.AsChannel().ToIEnumerable();
            Assert.AreEqual(src, result);
        }
    }
}
