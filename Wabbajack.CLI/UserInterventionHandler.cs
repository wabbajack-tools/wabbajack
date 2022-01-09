using System;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Steam.UserInterventions;

namespace Wabbajack.CLI;

public class UserInterventionHandler : IUserInterventionHandler
{
    public void Raise(IUserIntervention intervention)
    {
        if (intervention is GetAuthCode gac)
        {
            switch (gac.Type)
            {
                case GetAuthCode.AuthType.EmailCode:
                    Console.WriteLine("Please enter the Steam code that was just emailed to you");
                    break;
                case GetAuthCode.AuthType.TwoFactorAuth:
                    Console.WriteLine("Please enter your 2FA code for Steam");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            gac.Finish(Console.ReadLine()!.Trim());
        }
    }
}