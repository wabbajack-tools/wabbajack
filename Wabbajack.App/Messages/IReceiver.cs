namespace Wabbajack.App.Messages;

public interface IReceiverMarker
{
}

public interface IReceiver<in T> : IReceiverMarker
{
    public void Receive(T val);
}