using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.Compression.BSA.TES3Archive;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compression.BSA;

public static class BSADispatch
{
    private static readonly SignatureChecker BSASignatures = new(FileType.BSA, FileType.BA2, FileType.TES3);

    public static async ValueTask<IReader> Open(AbsolutePath filename)
    {
        return await BSASignatures.MatchesAsync(filename) switch
        {
            FileType.TES3 => await Reader.Load(new NativeFileStreamFactory(filename)),
            FileType.BSA => await TES5Archive.Reader.Load(new NativeFileStreamFactory(filename)),
            FileType.BA2 => await BA2Archive.Reader.Load(new NativeFileStreamFactory(filename)),
            _ => throw new InvalidDataException("Filename is not a .bsa or .ba2")
        };
    }

    public static async ValueTask<IReader> Open(IStreamFactory factory)
    {
        await using var stream = await factory.GetStream();
        return await BSASignatures.MatchesAsync(stream) switch
        {
            FileType.TES3 => await Reader.Load(factory),
            FileType.BSA => await TES5Archive.Reader.Load(factory),
            FileType.BA2 => await BA2Archive.Reader.Load(factory),
            _ => throw new InvalidDataException("Filename is not a .bsa or .ba2")
        };
    }

    public static async ValueTask<IReader> Open(IStreamFactory factory, FileType sig)
    {
        await using var stream = await factory.GetStream();
        return sig switch
        {
            FileType.TES3 => await Reader.Load(factory),
            FileType.BSA => await TES5Archive.Reader.Load(factory),
            FileType.BA2 => await BA2Archive.Reader.Load(factory),
            _ => throw new InvalidDataException("Filename is not a .bsa or .ba2")
        };
    }

    public static IBuilder CreateBuilder(IArchive oldState, TemporaryFileManager manager)
    {
        return oldState switch
        {
            TES3State tes3 => new Builder(tes3),
            BSAState bsa => TES5Archive.Builder.Create(bsa, manager),
            BA2State ba2 => BA2Archive.Builder.Create(ba2, manager),
            _ => throw new NotImplementedException()
        };
    }
}