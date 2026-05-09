using System;
using System.IO.Hashing;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Wabbajack.Hashing.xxHash64.Benchmark;

internal class Program
{
    private static void Main(string[] args)
    {
        BenchmarkRunner.Run<Base64EncoderBenchmark>();
    }
}

[MemoryDiagnoser]
[DisassemblyDiagnoser(3)]
public class xxHashBenchmark
{
    private readonly byte[] _data;

    public xxHashBenchmark()
    {
        _data = new byte[1024 * 1024];
    }

    [Benchmark]
    public ulong OneShot()
    {
        return XxHash64.HashToUInt64(_data);
    }

    [Benchmark]
    public ulong Incremental()
    {
        var hasher = new XxHash64();
        hasher.Append(_data);
        return hasher.GetCurrentHashAsUInt64();
    }
}

[MemoryDiagnoser]
[DisassemblyDiagnoser(3)]
public class Base64EncoderBenchmark
{
    private readonly Hash _data;

    public Base64EncoderBenchmark()
    {
        _data = new Hash(ulong.MaxValue >> 4);
    }

    [Benchmark]
    public void NewCode()
    {
        unsafe
        {
            Span<byte> buffer = stackalloc byte[12];
            _data.ToBase64(buffer);
            var result = Hash.FromBase64(buffer);
        }
    }

    [Benchmark]
    public void OldCode()
    {
        var result = Hash.FromBase64(_data.ToBase64());
    }
}
