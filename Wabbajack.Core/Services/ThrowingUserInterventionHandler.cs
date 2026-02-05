using System;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Services.OSIntegrated;

public class ThrowingUserInterventionHandler : IUserInterventionHandler
{
    public void Raise(IUserIntervention intervention)
    {
        throw new Exception("Unexpected user intervention, this should throw");
    }
}