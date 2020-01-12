using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    [TestClass]
    public class ABatchProcessorTests
    {
        #region CalculateThreadsToUse
        [TestMethod]
        public void Manual_OverRecommended()
        {
            Assert.AreEqual(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: true,
                manualMax: byte.MaxValue,
                targetUsage: 1.0d));
        }

        [TestMethod]
        public void Manual_NeedsTrimming()
        {
            Assert.AreEqual(5, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: true,
                manualMax: 5,
                targetUsage: 1.0d));
        }

        [TestMethod]
        public void Manual_Zero()
        {
            Assert.AreEqual(1, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: true,
                manualMax: 0,
                targetUsage: 1.0d));
        }

        [TestMethod]
        public void Auto_Full()
        {
            Assert.AreEqual(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 1.0d));
        }

        [TestMethod]
        public void Auto_Half()
        {
            Assert.AreEqual(4, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 0.5d));
        }

        [TestMethod]
        public void Auto_Zero()
        {
            Assert.AreEqual(1, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 0d));
        }

        [TestMethod]
        public void Auto_OverAllowed()
        {
            Assert.AreEqual(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 2d));
        }

        [TestMethod]
        public void Auto_UnderAllowed()
        {
            Assert.AreEqual(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: -2d));
        }
        #endregion
    }
}
