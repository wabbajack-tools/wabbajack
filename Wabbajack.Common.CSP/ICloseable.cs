namespace Wabbajack.Common.CSP
{
    public interface ICloseable
    {
        bool IsClosed { get; }
        void Close();
    }
}
