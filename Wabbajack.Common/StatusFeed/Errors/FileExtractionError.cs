namespace Wabbajack.Common.StatusFeed.Errors
{
    class FileExtractionError : AStatusMessage, IError
    {
        private string _filename;
        private string _destination;
        public override string ShortDescription { get; } = string.Empty;
        public override string ExtendedDescription { get; } = string.Empty;

        public FileExtractionError(string filename, string destination)
        {
            _filename = filename;
            _destination = destination;
        }
    }
}
