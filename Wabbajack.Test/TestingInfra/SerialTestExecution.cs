using TUnit.Core.Interfaces;
using Wabbajack.Test.TestingInfra;

// Many test groups (Compiler, Installer, VFS, FileExtractor, BSA, PHash, the download dispatcher)
// share on-disk caches and temp directories under KnownFolders.EntryPoint. TUnit runs tests in
// parallel by default, which races on that shared filesystem state. Cap concurrency at 1 for the
// whole assembly so behavior matches what the suite relied on under Xunit.DependencyInjection.
// (Phase 3 can relax this to per-class [NotInParallel] once tests use isolated temp roots.)
[assembly: ParallelLimiter<SerialTestExecution>]

namespace Wabbajack.Test.TestingInfra;

public sealed class SerialTestExecution : IParallelLimit
{
    public int Limit => 1;
}
