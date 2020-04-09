using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class WorkQueueTests
    {
        #region DynamicNumThreads
        const int Large = 8;
        const int Medium = 6;
        const int Small = 4;
        public TimeSpan PollMS => TimeSpan.FromSeconds(1);

        [Fact]
        public void DynamicNumThreads_Typical()
        {
            using (var queue = new WorkQueue())
            {
                Assert.Equal(Environment.ProcessorCount, queue.DesiredNumWorkers);
                Assert.Equal(Environment.ProcessorCount, queue._tasks.Count);
            }
        }

        [Fact]
        public void DynamicNumThreads_Increased()
        {
            var subj = new BehaviorSubject<int>(Small);
            using (var queue = new WorkQueue(subj))
            {
                Assert.Equal(Small, queue.DesiredNumWorkers);
                Assert.Equal(Small, queue._tasks.Count);
                subj.OnNext(Large);
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
            }
        }

        [Fact]
        public void DynamicNumThreads_EmptyObs()
        {
            using (var queue = new WorkQueue(Observable.Empty<int>()))
            {
                Assert.Equal(0, queue.DesiredNumWorkers);
                Assert.Empty(queue._tasks);
            }
        }

        [Fact]
        public async Task DynamicNumThreads_Decreased()
        {
            var subj = new BehaviorSubject<int>(Large);
            using (var queue = new WorkQueue(subj))
            {
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                subj.OnNext(Small);
                Assert.Equal(Small, queue.DesiredNumWorkers);
                // Tasks don't go down immediately
                Assert.Equal(Large, queue._tasks.Count);
                // After things re-poll, they should be cleaned
                await Task.Delay(PollMS * 2);
                Assert.Equal(Small, queue._tasks.Count);
            }
        }

        [Fact]
        public async Task DynamicNumThreads_IncreasedWhileWorking()
        {
            var subj = new BehaviorSubject<int>(Small);
            var tcs = new TaskCompletionSource<bool>();
            using (var queue = new WorkQueue(subj))
            {
                Assert.Equal(Small, queue.DesiredNumWorkers);
                Assert.Equal(Small, queue._tasks.Count);
                Enumerable.Range(0, Small).Do(_ => queue.QueueTask(() => tcs.Task));
                subj.OnNext(Large);
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                Task.Run(() => tcs.SetResult(true)).FireAndForget();
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                await Task.Delay(PollMS * 2);
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
            }
        }

        [Fact]
        public async Task DynamicNumThreads_DecreasedWhileWorking()
        {
            var subj = new BehaviorSubject<int>(Large);
            var tcs = new TaskCompletionSource<bool>();
            using (var queue = new WorkQueue(subj))
            {
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                Enumerable.Range(0, Large).Do(_ => queue.QueueTask(() => tcs.Task));
                subj.OnNext(Small);
                Assert.Equal(Small, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                // After things re-poll, they should still be working at max
                await Task.Delay(PollMS * 2);
                Assert.Equal(Large, queue._tasks.Count);
                // Complete, repoll, and check again
                Task.Run(() => tcs.SetResult(true)).FireAndForget();
                await Task.Delay(PollMS * 2);
                Assert.Equal(Small, queue._tasks.Count);
            }
        }

        [Fact]
        public async Task DynamicNumThreads_IncreasedThenDecreased()
        {
            var subj = new BehaviorSubject<int>(Small);
            using (var queue = new WorkQueue(subj))
            {
                Assert.Equal(Small, queue.DesiredNumWorkers);
                Assert.Equal(Small, queue._tasks.Count);
                subj.OnNext(Large);
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                subj.OnNext(Small);
                // Still large number of threads, as not immediate
                Assert.Equal(Small, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                // After things re-poll, they should still be working at max
                await Task.Delay(PollMS * 2);
                Assert.Equal(Small, queue.DesiredNumWorkers);
                Assert.Equal(Small, queue._tasks.Count);
            }
        }

        [Fact]
        public async Task DynamicNumThreads_DecreasedThenIncreased()
        {
            var subj = new BehaviorSubject<int>(Large);
            using (var queue = new WorkQueue(subj))
            {
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                subj.OnNext(Small);
                Assert.Equal(Small, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                subj.OnNext(Large);
                // New threads allocated immediately
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
                // After things re-poll, still here
                await Task.Delay(PollMS * 2);
                Assert.Equal(Large, queue.DesiredNumWorkers);
                Assert.Equal(Large, queue._tasks.Count);
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
        [Fact]
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
            var completed = await Task.WhenAny(Task.Delay(3000), task);
            Assert.Equal(completed, task);
        }
        #endregion
    }
}
