using System;
using System.Buffers.Text;

namespace Wabbajack.Hashing.xxHash64;

public struct Hash : IEquatable<Hash>, IComparable<Hash>
{
    private readonly ulong _code;

    public Hash(ulong code = 0)
    {
        _code = code;
    }

    public override string ToString()
    {
        return BitConverter.GetBytes(_code).ToBase64();
    }

    public bool Equals(Hash other)
    {
        return _code == other._code;
    }

    public int CompareTo(Hash other)
    {
        return _code.CompareTo(other._code);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Hash h)
            return h._code == _code;
        return false;
    }

    public override int GetHashCode()
    {
        return (int) (_code >> 32) ^ (int) _code;
    }

    public static bool operator ==(Hash a, Hash b)
    {
        return a._code == b._code;
    }

    public static bool operator !=(Hash a, Hash b)
    {
        return !(a == b);
    }

    public static explicit operator ulong(Hash a)
    {
        return a._code;
    }

    public static explicit operator long(Hash a)
    {
        return BitConverter.ToInt64(BitConverter.GetBytes(a._code));
    }

    public string ToBase64()
    {
        Span<byte> bytes = stackalloc byte[8];

        BitConverter.TryWriteBytes(bytes, _code);
        return Convert.ToBase64String(bytes);
    }

    public static Hash FromBase64(string hash)
    {
        return new Hash(BitConverter.ToUInt64(hash.FromBase64()));
    }

    public static Hash FromBase64(ReadOnlySpan<byte> data)
    {
        unsafe
        {
            Span<byte> buffer = stackalloc byte[12];
            Base64.DecodeFromUtf8(data, buffer, out var consumed, out var written);
            return new Hash(BitConverter.ToUInt64(buffer));
        }
    }

    public void ToBase64(Span<byte> output)
    {
        unsafe
        {
            Span<byte> buffer = stackalloc byte[8];
            if (!BitConverter.TryWriteBytes(buffer, _code))
                throw new Exception("Base64 Encoding error");
            Base64.EncodeToUtf8(buffer, output, out var consumed, out var written);
        }
    }

    public static Hash FromLong(in long argHash)
    {
        return new Hash(BitConverter.ToUInt64(BitConverter.GetBytes(argHash)));
    }

    public static Hash FromULong(in ulong argHash)
    {
        return new Hash(argHash);
    }

    public static Hash FromHex(string xxHashAsHex)
    {
        return new Hash(BitConverter.ToUInt64(xxHashAsHex.FromHex()));
    }

    public string ToHex()
    {
        return BitConverter.GetBytes(_code).ToHex();
    }

    public byte[] ToArray()
    {
        return BitConverter.GetBytes(_code);
    }

    public static Hash Interpret(string input)
    {
        return input.Length switch
        {
            16 => FromHex(input),
            12 when input.EndsWith('=') => FromBase64(input),
            _ => FromLong(long.Parse(input))
        };
    }

    public static bool TryGetFromHex(string hex, out Hash hash)
    {
        hash = default;
        if (hex.Length != 16) return false;
        try
        {
            hash = FromHex(hex);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}