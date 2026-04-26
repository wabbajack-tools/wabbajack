using Wabbajack.Compression.BSA.Interfaces;

namespace Wabbajack.Compression.BSA.BA2Archive;

internal interface IBA2FileEntry : IFile
{
    string FullPath { get; set; }
}