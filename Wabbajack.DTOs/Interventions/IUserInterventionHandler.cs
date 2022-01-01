namespace Wabbajack.DTOs.Interventions;

public interface IUserInterventionHandler
{
    public void Raise(IUserIntervention intervention);
}