using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Networking.Steam.UserInterventions;

public class GetUsernameAndPassword : IUserIntervention
{
    public void Cancel()
    {
        throw new NotImplementedException();
    }

    public bool Handled { get; }
    public CancellationToken Token { get; }
    
    public void SetException(Exception exception)
    {
        throw new NotImplementedException();
    }
}