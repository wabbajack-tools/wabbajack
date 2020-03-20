using System;
namespace Wabbajack.Common.Serialization {
public class UInt32Handler : IHandler {

    public void Write<T>(Serializer serializer, UInt32 data)
    {
        serializer.Writer.Write(data);
    }

    public T Read<T>(Deserializer deserializer)
    {
        return deserializer.Reader.ReadUInt32();
    }


}

public class Int32Handler : IHandler {

    public void Write<T>(Serializer serializer, Int32 data)
    {
        serializer.Writer.Write(data);
    }

    public T Read<T>(Deserializer deserializer)
    {
        return deserializer.Reader.ReadInt32();
    }


}


}