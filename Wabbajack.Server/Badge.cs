namespace Wabbajack.Server
{
    public class Badge
    {
        public int schemaVersion { get; set; } = 1;
        public string label { get; set; }
        public string message { get; set; }
        public string color { get; set; }

        public Badge(string _label, string _message)
        {
            label = _label;
            message = _message;
        }
    }
}
