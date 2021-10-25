using System.Data;
using System.IO;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.Texture;

public struct PHash
{
    public const int SIZE = 40;
    private readonly int _hash;

    public byte[] Data { get; }

    public PHash(byte[] data)
    {
        Data = data;
        if (Data.Length != SIZE)
            throw new DataException();

        long h = 0;
        h |= Data[0];
        h <<= 8;
        h |= Data[1];
        h <<= 8;
        h |= Data[2];
        h <<= 8;
        h |= Data[3];
        h <<= 8;
        _hash = (int) h;
    }

    public static PHash FromBase64(string base64)
    {
        var data = base64.FromBase64();
        if (data.Length != SIZE)
            throw new DataException();
        return new PHash(data);
    }

    public static PHash Read(BinaryReader br)
    {
        return new PHash(br.ReadBytes(SIZE));
    }

    public void Write(BinaryWriter br)
    {
        if (_hash == 0)
            br.Write(new byte[SIZE]);
        else
            br.Write(Data);
    }

    public override string ToString()
    {
        return Data.ToBase64();
    }

    public override int GetHashCode()
    {
        long h = 0;
        h |= Data[0];
        h <<= 8;
        h |= Data[1];
        h <<= 8;
        h |= Data[2];
        h <<= 8;
        h |= Data[3];
        h <<= 8;
        return (int) h;
    }
}