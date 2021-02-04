using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Wabbajack.Common;

namespace Wabbajack.Benchmark
{
    public class WorkQueueBenchmarks
    {
        private int[] _itms;
        private WorkQueue _queue;
        private TempFile _file;
        private Random _rdm;

        [Params(2, 4, 8, 16, 32, 64, 128, 256)]
        public int Threads { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            _rdm = new Random((int)DateTime.Now.ToFileTimeUtc());
            _itms = Enumerable.Range(0, Threads * 10).ToArray();
            _queue = new WorkQueue(Threads);

            _file = new TempFile();
            await using var f = await _file.Path.Create();
            var data = new byte[1024 * 1024 * 10]; // 1GB

            _rdm.NextBytes(data);
            await f.WriteAsync(data);
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            _queue.Dispose();
            await _file.DisposeAsync();
        }

       /* [Benchmark]
        public async Task SleepTask()
        {
            await _itms.PMap(_queue, async f => await Task.Delay(1));
        }*/
        
        [Benchmark]
        public async Task FileHashTask()
        {
            await _itms.PMap(_queue, async f => await _file.Path.FileHashAsync());
        }
    }
}
