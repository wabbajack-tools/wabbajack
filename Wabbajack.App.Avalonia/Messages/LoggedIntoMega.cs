using ReactiveUI;
using static CG.Web.MegaApiClient.MegaApiClient;

namespace Wabbajack.Messages;

public class LoggedIntoMega
{
    public AuthInfos Login { get; set; }
    public LoggedIntoMega(AuthInfos login)
    {
        Login = login;
    }
    public static void Send(AuthInfos login)
    {
        MessageBus.Current.SendMessage(new LoggedIntoMega(login));
    }
}