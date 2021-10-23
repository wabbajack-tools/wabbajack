using System;

namespace Wabbajack.Compression.BSA.TES3Archive;

[Flags]
public enum ArchiveFlags : uint
{
    HasFolderNames = 0x1,
    HasFileNames = 0x2,
    Compressed = 0x4,
    Unk4 = 0x8,
    Unk5 = 0x10,
    Unk6 = 0x20,
    XBox360Archive = 0x40,
    Unk8 = 0x80,
    HasFileNameBlobs = 0x100,
    Unk10 = 0x200,
    Unk11 = 0x400
}