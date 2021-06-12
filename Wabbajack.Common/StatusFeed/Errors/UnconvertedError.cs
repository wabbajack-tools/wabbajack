namespace Wabbajack.Common.StatusFeed.Errors
{
    public class UnconvertedError : AErrorMessage
    {
        private string _msg;

        public UnconvertedError(string msg)
        {
            _msg = msg;
        }

        public override string ShortDescription => _msg;
        public override string ExtendedDescription => _msg;
    }
}
