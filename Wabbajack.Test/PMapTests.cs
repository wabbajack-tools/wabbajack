using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class PMapTests
    {
        const int TypicalThreadCount = 6;
        const int TypicalDelayMS = 50;
        const int TimeoutSeconds = 15;

        [TestMethod]
        public async Task Typical_Action()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var output = new List<int>();
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        Thread.Sleep(TypicalDelayMS);
                        lock (output)
                        {
                            output.Add(item);
                        }
                    });
                await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(input.SequenceEqual(output.OrderBy(i => i)));
            }
        }

        [TestMethod]
        public async Task Typical_Func()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        Thread.Sleep(TypicalDelayMS);
                        return item.ToString();
                    });
                var results = await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(input.Select(i => i.ToString()).SequenceEqual(results));
            }
        }

        [TestMethod]
        public async Task Typical_Task()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var output = new List<int>();
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        await Task.Delay(TypicalDelayMS);
                        lock (output)
                        {
                            output.Add(item);
                        }
                    });
                await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(input.SequenceEqual(output.OrderBy(i => i)));
            }
        }

        [TestMethod]
        public async Task Typical_TaskReturn()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        await Task.Delay(TypicalDelayMS);
                        return item.ToString();
                    });
                var results = await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(input.Select(i => i.ToString()).SequenceEqual(results));
            }
        }

        [TestMethod]
        public async Task NestedAction()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var inputConstructedResults = input.SelectMany(i => Enumerable.Range(i * 100, TypicalThreadCount * 2));
                var output = new List<int>();
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        await Enumerable.Range(item * 100, TypicalThreadCount * 2)
                            .PMap(queue, async (subItem) =>
                            {
                                Assert.IsTrue(WorkQueue.WorkerThread);
                                Thread.Sleep(TypicalDelayMS);
                                lock (output)
                                {
                                    output.Add(subItem);
                                }
                            });
                    });
                await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(inputConstructedResults.SequenceEqual(output.OrderBy(i => i)));
            }
        }

        [TestMethod]
        public async Task Nested_Func()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var inputConstructedResults = input.SelectMany(i => Enumerable.Range(i * 100, TypicalThreadCount * 2));
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        return await Enumerable.Range(item * 100, TypicalThreadCount * 2)
                            .PMap(queue, (subItem) =>
                            {
                                Assert.IsTrue(WorkQueue.WorkerThread);
                                Thread.Sleep(TypicalDelayMS);
                                return subItem;
                            });
                    });
                var results = await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(inputConstructedResults.SequenceEqual(results.SelectMany(i => i)));
            }
        }

        [TestMethod]
        public async Task Nested_Task()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var inputConstructedResults = input.SelectMany(i => Enumerable.Range(i * 100, TypicalThreadCount * 2));
                var output = new List<int>();
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        await Enumerable.Range(item * 100, TypicalThreadCount * 2)
                            .PMap(queue, async (subItem) =>
                            {
                                Assert.IsTrue(WorkQueue.WorkerThread);
                                await Task.Delay(TypicalDelayMS);
                                lock (output)
                                {
                                    output.Add(subItem);
                                }
                            });
                    });
                await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(inputConstructedResults.SequenceEqual(output.OrderBy(i => i)));
            }
        }

        [TestMethod]
        public async Task Nested_TaskReturn()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var inputConstructedResults = input.SelectMany(i => Enumerable.Range(i * 100, TypicalThreadCount * 2));
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        return await Enumerable.Range(item * 100, TypicalThreadCount * 2)
                            .PMap(queue, async (subItem) =>
                            {
                                Assert.IsTrue(WorkQueue.WorkerThread);
                                await Task.Delay(TypicalDelayMS);
                                return subItem;
                            });
                    });
                var results = await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(inputConstructedResults.SequenceEqual(results.SelectMany(i => i)));
            }
        }

        [TestMethod]
        public async Task Nested_BackgroundThreadsInvolved()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var inputConstructedResults = input.SelectMany(i => Enumerable.Range(i * 100, TypicalThreadCount * 2));
                var workTask = Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        return await Enumerable.Range(item * 100, TypicalThreadCount * 2)
                            .PMap(queue, async (subItem) =>
                            {
                                return await Task.Run(async () =>
                                {
                                    Assert.IsTrue(WorkQueue.WorkerThread);
                                    await Task.Delay(TypicalDelayMS);
                                    return subItem;
                                });
                            });
                    });
                var results = await workTask.TimeoutButContinue(TimeSpan.FromSeconds(TimeoutSeconds), () => throw new TimeoutException());
                Assert.IsTrue(inputConstructedResults.SequenceEqual(results.SelectMany(i => i)));
            }
        }
    }
}
