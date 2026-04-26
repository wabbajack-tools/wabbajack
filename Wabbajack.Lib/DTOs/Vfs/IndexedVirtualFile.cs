using System.Collections.Generic;
using Wabbajack.DTOs.Texture;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Vfs;

public class IndexedVirtualFile
{
    public IPath Name { get; set; }
    public Hash Hash { get; set; }

    public ImageState? ImageState { get; set; }
    public long Size { get; set; }
    public List<IndexedVirtualFile> Children { get; set; } = new();
}