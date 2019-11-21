namespace Wabbajack.Common.CSP
{
    public interface IChannel<TIn, TOut> : IReadPort<TOut>, IWritePort<TIn>
    {
    }
}
