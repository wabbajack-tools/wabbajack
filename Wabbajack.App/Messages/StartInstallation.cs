using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.App.Messages
{
    public record StartInstallation(AbsolutePath ModListPath, AbsolutePath Install, AbsolutePath Download)
    {
    }
}