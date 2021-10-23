using System;

namespace Wabbajack.Compression.BSA.TES3Archive;

[Flags]
public enum FileFlags : uint
{
    Meshes = 0x1,
    Textures = 0x2,
    Menus = 0x4,
    Sounds = 0x8,
    Voices = 0x10,
    Shaders = 0x20,
    Trees = 0x40,
    Fonts = 0x80,
    Miscellaneous = 0x100
}