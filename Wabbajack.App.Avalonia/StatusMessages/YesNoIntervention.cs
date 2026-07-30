namespace Wabbajack;

public class YesNoIntervention : ConfirmationIntervention
{
    public YesNoIntervention(string description, string title)
    {
        ExtendedDescription = description;
        ShortDescription = title;
    }
    public override string ShortDescription { get; }
    public override string ExtendedDescription { get; }
}
