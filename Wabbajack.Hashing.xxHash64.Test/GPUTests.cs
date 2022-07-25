using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using Wabbajack.Hashing.xxHash64.GPU;
using Xunit;

namespace Wabbajack.Hashing.xxHash64.Test;

public class GPUTests
{
    static GPUTests()
    {
        CurrentContext = Context.Create(b => b.Default().StaticFields(StaticFieldMode.MutableStaticFields | StaticFieldMode.IgnoreStaticFieldStores)); 
    }

    [Theory]
    [MemberData(nameof(Accelerators))]
    public void CanHashData(Accelerator acc, byte[] data)
    {
        var result = Algorithm.HashBytes(acc, data);
        Assert.Equal(Hash.FromBase64("vBY6OyblpIw="), Hash.FromULong(result));
    }
    

    public static Context CurrentContext { get; set; }


    public static IEnumerable<object[]> Accelerators()
    {
        var random = new Random(42);
        var data = new byte[1024 * 1024 * 1024];
        random.NextBytes(data);
        return CurrentContext.Devices.Select(c => { return new object[] {c.CreateAccelerator(CurrentContext), data}; });
    }
}