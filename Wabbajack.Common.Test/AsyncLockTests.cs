using System;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class AsyncLockTests
    {
        [Fact]
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
                    Assert.True(firstRun);
                }
            });
            await Task.WhenAll(first, second);
        }

        [Fact]
        public async Task Exception()
        {
            var asyncLock = new AsyncLock();
            bool firstRun = false;
            bool secondRun = false;
            // Throw exception inside a lock
            await Assert.ThrowsAsync<Exception>(() =>
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
                        Assert.True(firstRun);
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
