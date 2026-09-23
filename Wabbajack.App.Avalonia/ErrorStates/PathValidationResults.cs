namespace Wabbajack
{
    /// <summary>
    ///     A validation failure that belongs to the install folder picker. As in WPF, the static
    ///     <c>Fail</c> it is called through is <see cref="ValidationResult" />'s and returns the base type, so
    ///     nothing is ever actually of this type and the pickers never pick these up; kept as WPF has it.
    /// </summary>
    public class InstallPathValidationResult : ValidationResult
    {
        public override string ToString()
        {
            return $"({(Succeeded ? "Success" : "Fail")}, {Reason})";
        }
    }

    /// <summary>The downloads folder picker's counterpart of <see cref="InstallPathValidationResult" />.</summary>
    public class DownloadsPathValidationResult : ValidationResult
    {
        public override string ToString()
        {
            return $"({(Succeeded ? "Success" : "Fail")}, {Reason})";
        }
    }
}
