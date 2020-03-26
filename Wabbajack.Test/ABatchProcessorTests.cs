using Wabbajack.Lib;
using Xunit;

namespace Wabbajack.Test
{
    public class ABatchProcessorTests
    {
        #region CalculateThreadsToUse
        [Fact]
        public void Manual_OverRecommended()
        {
            Assert.Equal(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: true,
                manualMax: byte.MaxValue,
                targetUsage: 1.0d));
        }

        [Fact]
        public void Manual_NeedsTrimming()
        {
            Assert.Equal(5, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: true,
                manualMax: 5,
                targetUsage: 1.0d));
        }

        [Fact]
        public void Manual_Zero()
        {
            Assert.Equal(1, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: true,
                manualMax: 0,
                targetUsage: 1.0d));
        }

        [Fact]
        public void Auto_Full()
        {
            Assert.Equal(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 1.0d));
        }

        [Fact]
        public void Auto_Half()
        {
            Assert.Equal(4, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 0.5d));
        }

        [Fact]
        public void Auto_Zero()
        {
            Assert.Equal(1, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 0d));
        }

        [Fact]
        public void Auto_OverAllowed()
        {
            Assert.Equal(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: 2d));
        }

        [Fact]
        public void Auto_UnderAllowed()
        {
            Assert.Equal(8, ABatchProcessor.CalculateThreadsToUse(
                recommendedCount: 8,
                manual: false,
                manualMax: byte.MaxValue,
                targetUsage: -2d));
        }
        #endregion
    }
}
