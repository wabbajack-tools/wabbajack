using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Wabbajack.Test
{
    [TestClass]
    public class TaskExtTests
    {
        [TestMethod]
        public async Task TimeoutButContinue_Typical()
        {
            bool timedOut = false;
            await Task.Delay(100).TimeoutButContinue(TimeSpan.FromSeconds(1), () => timedOut = true);
            Assert.IsFalse(timedOut);
        }

        [TestMethod]
        public async Task TimeoutButContinue_TimedOut()
        {
            bool timedOut = false;
            await Task.Delay(300).TimeoutButContinue(TimeSpan.FromMilliseconds(100), () => timedOut = true);
            Assert.IsTrue(timedOut);
        }
    }
}
