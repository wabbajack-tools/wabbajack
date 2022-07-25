namespace Wabbajack.Hashing.xxHash64.GPU;

public struct AlgorithmState
{
    private static readonly IReadOnlyList<ulong> Primes64 =
        new[]
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
    
    
    internal readonly ulong Seed;

    internal ulong A;
    internal ulong B;
    internal ulong C;
    internal ulong D;

    internal ulong BytesProcessed;

    public AlgorithmState(ulong seed)
    {
        Seed = seed;
        A = Seed + Primes64[0] + Primes64[1];
        B = Seed + Primes64[1];
        C = Seed;
        D = Seed - Primes64[0];
        BytesProcessed = 0;
    }
}