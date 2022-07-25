using ILGPU;
using ILGPU.Runtime;
using ILGPU.Util;

namespace Wabbajack.Hashing.xxHash64.GPU;

public class Algorithm
{
    private static readonly ulong[] Primes64 =
        {
            11400714785074694791UL,
            14029467366897019727UL,
            1609587929392839161UL,
            9650029242287828579UL,
            2870177450012600261UL
        };
    
    private const ulong Prime0 = 11400714785074694791UL;
    private const ulong Prime1 = 14029467366897019727UL;
    private const ulong Prime2 = 1609587929392839161UL;
    private const ulong Prime3 = 9650029242287828579UL;
    private const ulong Prime4 = 2870177450012600261UL;

    private const ulong Seed = 0L;

    public ulong HashBytes(byte[] data)
    {
        return HashBytes(Accelerator.Current, data);
    }
    
    public static ulong HashBytes(Accelerator accelerator, byte[] data)
    {
        var initialSize = (data.Length >> 5) << 5;

        var gpuData = accelerator.Allocate1D<byte>(initialSize);
        gpuData.CopyFromCPU(data);

        ulong seed = 0;
        
        var state = accelerator.Allocate1D<ulong>(4);
        var tmpState = new ulong[4];
        tmpState[0] = seed + Primes64[0] + Primes64[1];
        tmpState[1] = seed + Primes64[1];
        tmpState[2] = seed;
        tmpState[3] = seed - Primes64[0];
        state.CopyFromCPU(tmpState);

        if (initialSize > 0)
        {
            var transformKernal = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<ulong>, ArrayView<byte>, int>(TransformByteGroupsInternal);
            transformKernal(new Index1D(4), state.View, gpuData.View, initialSize);
        }

        var cpuData = new ulong[4];
        state.View.CopyToCPU(cpuData);
        return FinalizeHashValueInternal(cpuData, data.AsSpan(initialSize..), (ulong)initialSize);
    }
    
    private static void TransformByteGroupsInternal(Index1D index, ArrayView<ulong> state, ArrayView<byte> dataIn, int size)
    {
        var data = dataIn.Cast<ulong>();
        var temp = state[index.X];

        var tempPrime0 = Prime0;
        var tempPrime1 = Prime1;

        for (var idx = index.X; idx < data.Length; idx += 4)
        {
            temp += data[idx] * tempPrime1;
            temp = RotateLeft(temp, 31);
            temp *= tempPrime0;
        }

        state[index.X] = temp;
    }
    
    private static ulong FinalizeHashValueInternal(ulong[] hashState, ReadOnlySpan<byte> data, ulong bytesProcessed)
    {
        ulong hashValue;
        {
            if (bytesProcessed > 0)
            {
                var tempA = hashState[0];
                var tempB = hashState[1];
                var tempC = hashState[2];
                var tempD = hashState[3];


                hashValue = RotateLeft(tempA, 1) + RotateLeft(tempB, 7) + RotateLeft(tempC, 12) + RotateLeft(tempD, 18);

                // A
                tempA *= Primes64[1];
                tempA = RotateLeft(tempA, 31);
                tempA *= Primes64[0];

                hashValue ^= tempA;
                hashValue = hashValue * Primes64[0] + Primes64[3];

                // B
                tempB *= Primes64[1];
                tempB = RotateLeft(tempB, 31);
                tempB *= Primes64[0];

                hashValue ^= tempB;
                hashValue = hashValue * Primes64[0] + Primes64[3];

                // C
                tempC *= Primes64[1];
                tempC = RotateLeft(tempC, 31);
                tempC *= Primes64[0];

                hashValue ^= tempC;
                hashValue = hashValue * Primes64[0] + Primes64[3];

                // D
                tempD *= Primes64[1];
                tempD = RotateLeft(tempD, 31);
                tempD *= Primes64[0];

                hashValue ^= tempD;
                hashValue = hashValue * Primes64[0] + Primes64[3];
            }
            else
            {
                hashValue = Seed + Primes64[4];
            }
        }

        var remainderLength = data.Length;

        hashValue += bytesProcessed + (ulong) remainderLength;

        if (remainderLength > 0)
        {
            // In 8-byte chunks, process all full chunks
            for (var x = 0; x < data.Length / 8; ++x)
            {
                hashValue ^= RotateLeft(BitConverter.ToUInt64(data[(x * 8)..]) * Primes64[1], 31) * Primes64[0];
                hashValue = RotateLeft(hashValue, 27) * Primes64[0] + Primes64[3];
            }

            // Process a 4-byte chunk if it exists
            if (remainderLength % 8 >= 4)
            {
                var startOffset = remainderLength - remainderLength % 8;

                hashValue ^= BitConverter.ToUInt32(data[startOffset..]) * Primes64[0];
                hashValue = RotateLeft(hashValue, 23) * Primes64[1] + Primes64[2];
            }

            // Process last 4 bytes in 1-byte chunks (only runs if data.Length % 4 != 0)
            {
                var startOffset = remainderLength - remainderLength % 4;
                var endOffset = remainderLength;

                for (var currentOffset = startOffset; currentOffset < endOffset; currentOffset += 1)
                {
                    hashValue ^= data[currentOffset] * Primes64[4];
                    hashValue = RotateLeft(hashValue, 11) * Primes64[0];
                }
            }
        }

        hashValue ^= hashValue >> 33;
        hashValue *= Primes64[1];
        hashValue ^= hashValue >> 29;
        hashValue *= Primes64[2];
        hashValue ^= hashValue >> 32;

        return hashValue;
    }

    private static ulong RotateLeft(ulong operand, int shiftCount)
    {
        shiftCount &= 0x3f;

        return
            (operand << shiftCount) |
            (operand >> (64 - shiftCount));
    }
}