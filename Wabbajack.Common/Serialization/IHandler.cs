namespace Wabbajack.Common.Serialization
{
    public interface IHandler
    {
        public void Write<T>(Serializer serializer, T data);
        public T Read<T>(Deserializer deserialiser);

    }
}
