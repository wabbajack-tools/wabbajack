using System;
using System.Data.HashFunction.xxHash;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using Wabbajack.Hashing.xxHash64.GPU;

namespace Wabbajack.Hashing.xxHash64.Benchmark;

internal class Program
{
    private static void Main(string[] args)
    {
        BenchmarkRunner.Run<xxHashBenchmark>();
    }
}

[MemoryDiagnoser]
[DisassemblyDiagnoser(3)]
public class xxHashBenchmark
{
    private readonly byte[] _data;
    private readonly Context _context;
    private readonly Accelerator _gpu;
    private readonly Accelerator _cpu;

    public xxHashBenchmark()
    {
        _data = new byte[1024 * 1024 * 1024];

        _context = Context.CreateDefault();
        _gpu = _context.GetPreferredDevice(false).CreateAccelerator(_context);
        _cpu = _context.GetPreferredDevice(true).CreateAccelerator(_context);
    }

    [Benchmark]
    public void NewCode()
    {
        var hash = new xxHashAlgorithm(0);
        hash.HashBytes(_data);
    }

    [Benchmark]
    public void OldCode()
    {
        var config = new xxHashConfig {HashSizeInBits = 64};
        BitConverter.ToUInt64(xxHashFactory.Instance.Create(config).ComputeHash(_data).Hash);
    }

    [Benchmark]
    public void GPUCode()
    {
        Algorithm.HashBytes(_gpu, _data);
    }
    
    [Benchmark]
    public void CPUCode()
    {
        Algorithm.HashBytes(_cpu, _data);
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