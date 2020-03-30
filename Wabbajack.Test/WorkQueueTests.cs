using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class WorkQueueTests
    {
        #region DynamicNumThreads
        const int Large = 8;
        const int Medium = 6;
        const int Small = 4;
        public TimeSpan PollMS => TimeSpan.FromSeconds(1);

        [TestMethod]
        public void DynamicNumThreads_Typical()
        {
            using (var queue = new WorkQueue())
            {
                Assert.AreEqual(Environment.ProcessorCount, queue.DesiredNumWorkers);
                Assert.AreEqual(Environment.ProcessorCount, queue._tasks.Count);
            }
        }

        [TestMethod]
        public void DynamicNumThreads_Increased()
        {
            var subj = new BehaviorSubject<int>(Small);
            using (var queue = new WorkQueue(subj))
            {
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                Assert.AreEqual(Small, queue._tasks.Count);
                subj.OnNext(Large);
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
            }
        }

        [TestMethod]
        public void DynamicNumThreads_EmptyObs()
        {
            using (var queue = new WorkQueue(Observable.Empty<int>()))
            {
                Assert.AreEqual(0, queue.DesiredNumWorkers);
                Assert.AreEqual(0, queue._tasks.Count);
            }
        }

        [TestMethod]
        public async Task DynamicNumThreads_Decreased()
        {
            var subj = new BehaviorSubject<int>(Large);
            using (var queue = new WorkQueue(subj))
            {
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                subj.OnNext(Small);
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                // Tasks don't go down immediately
                Assert.AreEqual(Large, queue._tasks.Count);
                // After things re-poll, they should be cleaned
                await Task.Delay(PollMS * 2);
                Assert.AreEqual(Small, queue._tasks.Count);
            }
        }

        [TestMethod]
        public async Task DynamicNumThreads_IncreasedWhileWorking()
        {
            var subj = new BehaviorSubject<int>(Small);
            var tcs = new TaskCompletionSource<bool>();
            using (var queue = new WorkQueue(subj))
            {
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                Assert.AreEqual(Small, queue._tasks.Count);
                Enumerable.Range(0, Small).Do(_ => queue.QueueTask(() => tcs.Task));
                subj.OnNext(Large);
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                Task.Run(() => tcs.SetResult(true)).FireAndForget();
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                await Task.Delay(PollMS * 2);
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
            }
        }

        [TestMethod]
        public async Task DynamicNumThreads_DecreasedWhileWorking()
        {
            var subj = new BehaviorSubject<int>(Large);
            var tcs = new TaskCompletionSource<bool>();
            using (var queue = new WorkQueue(subj))
            {
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                Enumerable.Range(0, Large).Do(_ => queue.QueueTask(() => tcs.Task));
                subj.OnNext(Small);
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                // After things re-poll, they should still be working at max
                await Task.Delay(PollMS * 2);
                Assert.AreEqual(Large, queue._tasks.Count);
                // Complete, repoll, and check again
                Task.Run(() => tcs.SetResult(true)).FireAndForget();
                await Task.Delay(PollMS * 2);
                Assert.AreEqual(Small, queue._tasks.Count);
            }
        }

        [TestMethod]
        public async Task DynamicNumThreads_IncreasedThenDecreased()
        {
            var subj = new BehaviorSubject<int>(Small);
            using (var queue = new WorkQueue(subj))
            {
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                Assert.AreEqual(Small, queue._tasks.Count);
                subj.OnNext(Large);
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                subj.OnNext(Small);
                // Still large number of threads, as not immediate
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                // After things re-poll, they should still be working at max
                await Task.Delay(PollMS * 2);
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                Assert.AreEqual(Small, queue._tasks.Count);
            }
        }

        [TestMethod]
        public async Task DynamicNumThreads_DecreasedThenIncreased()
        {
            var subj = new BehaviorSubject<int>(Large);
            using (var queue = new WorkQueue(subj))
            {
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                subj.OnNext(Small);
                Assert.AreEqual(Small, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                subj.OnNext(Large);
                // New threads allocated immediately
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
                // After things re-poll, still here
                await Task.Delay(PollMS * 2);
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);
            }
        }
        #endregion

        #region Known Deadlock Scenario
        /// <summary>
        /// Known "deadlock" scenario related to WorkQueue.
        /// 
        /// When a task is completed via a TaskCompletionSource, the current thread is "in charge" of running the continuation code that
        /// completing that task kicked off.  The problem with this when related to WorkQueue is that it's an infinite while loop of continuation.
        /// 
        /// The solution to this is just make sure that any work done relating to WorkQueue be done within its own Task.Run() call, so that if it that thread 
        /// "takes over" a workqueue loop, it doesn't matter as it was a threadpool thread anyway.
        /// </summary>
        [TestMethod]
        public async Task Deadlock()
        {
            var task = Task.Run(async () =>
            {
                var subj = new BehaviorSubject<int>(Large);
                var tcs = new TaskCompletionSource<bool>();
                using (var queue = new WorkQueue(subj))
                {
                    Enumerable.Range(0, Large).Do(_ => queue.QueueTask(() => tcs.Task));
                    // This call deadlocks, as the continuations is a WorkQueue while loop
                    tcs.SetResult(true);
                }
            });
            var completed = Task.WhenAny(Task.Delay(3000), task);
            Assert.ReferenceEquals(completed, task);
        }
        #endregion

        #region Known Parallel Work Collapse Pitfall
        /// <summary>
        /// Putting a single TCS completion source onto the WorkQueue will result in parallization collapse, where
        /// all work is being done by one actual thread.  Similar to the deadlock scenario, this is just slightly different.
        /// 
        /// Since all worker tasks in charge of pulling off the queue were working on a single job driven by a single TCS,
        /// when that TCS completes, the one thread that completed it is in charge of all the continuation.  All the continuation
        /// tasks happen to be all Tasks in charge of pulling off the queue.  This results in one actual thread essentially calling a
        /// Task.WhenAll() on all of our queue.Take tasks.  This means only one thread is now ping-ponging around doing the work, rather
        /// than our desired number of threads working in parallel.
        /// 
        /// This will happen even if the WorkQueue is backed by Threads, rather than Task.Run() calls.  It's just the nature of how async
        /// continuation is wired to work.
        /// 
        /// Other notes:
        ///   This seems to fail when run in the normal pipeline of unit tests.  I think the timing gets interrupted by other tests?
        ///   Disabled the test from being run automatically for now
        /// 
        /// TLDR:  Don't put the same work completion source to be done on the queue multiple times.
        /// </summary>
        public async Task ThreadCoalescenceExample()
        {
            var subj = new BehaviorSubject<int>(Large);
            var tcs = new TaskCompletionSource<bool>();
            object lockObj = new object();
            using (var queue = new WorkQueue(subj))
            {
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);

                bool[] workStartedArray = new bool[Large];
                async Task Job(int num, bool[] b)
                {
                    // Mark work started as soon as job started
                    lock (lockObj)
                    {
                        b[num] = true;
                    }
                    // Do lots of hard work for 1 second
                    Thread.Sleep(5000);
                };

                // Do hard work in parallel
                Enumerable.Range(0, Large).Do(i => queue.QueueTask(() => Job(i, workStartedArray)));
                // Wait some time, so all jobs should be started
                await Task.Delay(2500);
                // Show that all jobs are started
                lock (lockObj)
                {
                    Assert.AreEqual(Large, workStartedArray.Where(i => i).Count());
                }

                await Task.Delay(15000);

                // Start lots of jobs, all pinning from the same TCS
                Enumerable.Range(0, Large).Do(_ => queue.QueueTask(() => tcs.Task));
                // All 8 worker tasks are completed by the same TCS, but continued by the single Task
                // that kicked it off and is in charge of the continuation tasks.
                // Parallel worker Tasks have now coalesced into a single thread
                Task.Run(() => tcs.SetResult(true)).FireAndForget();
                Assert.AreEqual(Large, queue.DesiredNumWorkers);
                Assert.AreEqual(Large, queue._tasks.Count);

                await Task.Delay(10000);

                // Do a test to prove work isn't being done in parallel anymore
                var secondWorkStartedArray = new bool[Large];
                Enumerable.Range(0, Large).Do(i => queue.QueueTask(() => Job(i, secondWorkStartedArray)));
                // Wait some time, so all jobs should be started
                await Task.Delay(2500);
                // Show that only one job was started/worked on (by our one coalesced worker thread)
                lock (lockObj)
                {
                    Assert.AreEqual(1, secondWorkStartedArray.Where(i => i).Count());
                }
            }
        }
        #endregion
    }
}
