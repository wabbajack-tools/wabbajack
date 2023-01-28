using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Wabbajack.DTOs.Texture;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.VFS;

public static class IndexedVirtualFileExtensions
{
    public static void Write(this IndexedVirtualFile ivf, BinaryWriter bw)
    {
        bw.Write(ivf.Name.ToString()!);
        bw.Write((ulong) ivf.Hash);

        if (ivf.ImageState == null)
        {
            bw.Write(false);
        }
        else
        {
            bw.Write(true);
            WriteImageState(bw, ivf.ImageState);
        }

        bw.Write(ivf.Size);
        bw.Write(ivf.Children.Count);
        foreach (var file in ivf.Children)
            file.Write(bw);
    }

    private static void WriteImageState(BinaryWriter bw, ImageState state)
    {
        bw.Write((ushort) state.Width);
        bw.Write((ushort) state.Height);
        bw.Write((byte)state.MipLevels);
        bw.Write((byte) state.Format);
        state.PerceptualHash.Write(bw);
    }

    static ImageState ReadImageState(BinaryReader br)
    {
        return new ImageState
        {
            Width = br.ReadUInt16(),
            Height = br.ReadUInt16(),
            MipLevels = br.ReadByte(),
            Format = (DXGI_FORMAT) br.ReadByte(),
            PerceptualHash = PHash.Read(br)
        };
    }


    public static void Write(this IndexedVirtualFile ivf, Stream s)
    {
        using var cs = new GZipStream(s, CompressionLevel.Optimal, true);
        using var bw = new BinaryWriter(cs, Encoding.UTF8, true);
        ivf.Write(bw);
    }

    public static IndexedVirtualFile Read(BinaryReader br)
    {
        var ivf = new IndexedVirtualFile
        {
            Name = (RelativePath) br.ReadString(),
            Hash = Hash.FromULong(br.ReadUInt64())
        };

        if (br.ReadBoolean())
            ivf.ImageState = ReadImageState(br);

        ivf.Size = br.ReadInt64();

        var lst = new List<IndexedVirtualFile>();
        ivf.Children = lst;
        var count = br.ReadInt32();
        for (var x = 0; x < count; x++) lst.Add(Read(br));

        return ivf;
    }

    public static IndexedVirtualFile Read(Stream s)
    {
        using var cs = new GZipStream(s, CompressionMode.Decompress, true);
        using var br = new BinaryReader(cs);
        return Read(br);
    }
}