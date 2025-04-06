namespace Wabbajack
{
    public class DownloadsPathValidationResult : ValidationResult
    {
        public override string ToString()
        {
            return $"({(Succeeded ? "Success" : "Fail")}, {Reason})";
        }
    }
}
