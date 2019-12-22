using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class AsyncLockTests
    {
        [TestMethod]
        public async Task Typical()
        {
            var asyncLock = new AsyncLock();
            bool firstRun = false;
            var first = Task.Run(async () =>
            {
                using (await asyncLock.Wait())
                {
                    await Task.Delay(500);
                    firstRun = true;
                }
            });
            var second = Task.Run(async () =>
            {
                await Task.Delay(200);
                using (await asyncLock.Wait())
                {
                    Assert.IsTrue(firstRun);
                }
            });
            await Task.WhenAll(first, second);
        }

        [TestMethod]
        public async Task Exception()
        {
            var asyncLock = new AsyncLock();
            bool firstRun = false;
            bool secondRun = false;
            // Throw exception inside a lock
            await Assert.ThrowsExceptionAsync<Exception>(() =>
            {
                return Task.Run(async () =>
                {
                    using (await asyncLock.Wait())
                    {
                        await Task.Delay(500);
                        firstRun = true;
                        throw new Exception();
                    }
                });
            });

            await Task.WhenAll(
                // Try to re-enter lock afterwards
                Task.Run(async () =>
                {
                    await Task.Delay(200);
                    using (await asyncLock.Wait())
                    {
                        Assert.IsTrue(firstRun);
                        secondRun = true;
                    }
                }),
                // Add a timeout to fail if we cannot
                Task.Run(async () =>
                {
                    await Task.Delay(500);
                    if (!secondRun)
                    {
                        throw new ArgumentException();
                    }
                }));
        }
    }
}
