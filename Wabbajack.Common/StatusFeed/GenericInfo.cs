namespace Wabbajack.Common.StatusFeed
{
    public class GenericInfo : AStatusMessage, IInfo
    {
        public override string ShortDescription { get; }
        private readonly string _extendedDescription;
        public override string ExtendedDescription => _extendedDescription ?? ShortDescription;

        public GenericInfo(string short_description, string? long_description = null)
        {
            ShortDescription = short_description;
            _extendedDescription = long_description ?? string.Empty;
        }

        public override string ToString()
        {
            return ShortDescription;
        }
    }
}
