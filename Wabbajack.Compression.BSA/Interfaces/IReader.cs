using System.Collections.Generic;
using Wabbajack.DTOs.BSA.ArchiveStates;

namespace Wabbajack.Compression.BSA.Interfaces;

public interface IReader
{
    /// <summary>
    ///     The files defined by the archive
    /// </summary>
    IEnumerable<IFile> Files { get; }

    IArchive State { get; }
}