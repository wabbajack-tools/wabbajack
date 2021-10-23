using Wabbajack.Compression.BSA.Interfaces;

namespace Wabbajack.Compression.BSA.FO4Archive;

internal interface IBA2FileEntry : IFile
{
    string FullPath { get; set; }
}