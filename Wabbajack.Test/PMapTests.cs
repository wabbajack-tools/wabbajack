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
        const int TypicalThreadCount = 2;
        const int TypicalDelayMS = 50;

        [TestMethod]
        public async Task Typical_Action()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var output = new List<int>();
                await Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        Thread.Sleep(TypicalDelayMS);
                        lock (output)
                        {
                            output.Add(item);
                        }
                    });
                Assert.IsTrue(input.SequenceEqual(output.OrderBy(i => i)));
            }
        }

        [TestMethod]
        public async Task Typical_Func()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var results = await Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        Thread.Sleep(TypicalDelayMS);
                        return item.ToString();
                    });
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
                await Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        await Task.Delay(TypicalDelayMS);
                        lock (output)
                        {
                            output.Add(item);
                        }
                    });
                Assert.IsTrue(input.SequenceEqual(output.OrderBy(i => i)));
            }
        }

        [TestMethod]
        public async Task Typical_TaskReturn()
        {
            using (var queue = new WorkQueue(TypicalThreadCount))
            {
                var input = Enumerable.Range(0, TypicalThreadCount * 2).ToArray();
                var results = await Enumerable.Range(0, TypicalThreadCount * 2)
                    .PMap(queue, async (item) =>
                    {
                        Assert.IsTrue(WorkQueue.WorkerThread);
                        await Task.Delay(TypicalDelayMS);
                        return item.ToString();
                    });
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
                await Enumerable.Range(0, TypicalThreadCount * 2)
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
                var results = await Enumerable.Range(0, TypicalThreadCount * 2)
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
                await Enumerable.Range(0, TypicalThreadCount * 2)
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
                var results = await Enumerable.Range(0, TypicalThreadCount * 2)
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
                var results = await Enumerable.Range(0, TypicalThreadCount * 2)
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
                Assert.IsTrue(inputConstructedResults.SequenceEqual(results.SelectMany(i => i)));
            }
        }
    }
}
