using System;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class TaskExtTests
    {
        [Fact]
        public async Task TimeoutButContinue_Typical()
        {
            bool timedOut = false;
            await Task.Delay(100).TimeoutButContinue(TimeSpan.FromSeconds(1), () => timedOut = true);
            Assert.False(timedOut);
        }

        [Fact]
        public async Task TimeoutButContinue_TimedOut()
        {
            bool timedOut = false;
            await Task.Delay(3000).TimeoutButContinue(TimeSpan.FromMilliseconds(100), () => timedOut = true);
            Assert.True(timedOut);
        }
    }
}
