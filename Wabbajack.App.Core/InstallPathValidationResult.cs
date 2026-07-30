namespace Wabbajack
{
    public class InstallPathValidationResult : ValidationResult
    {
        public override string ToString()
        {
            return $"({(Succeeded ? "Success" : "Fail")}, {Reason})";
        }
    }
}
