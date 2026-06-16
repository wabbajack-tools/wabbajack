using Wabbajack.Installer;

namespace Wabbajack;

/// <summary>
/// Platform abstraction for gathering <see cref="SystemParameters"/> (screen size, video/system memory,
/// GPU name, etc.). The WPF app supplies a Windows PInvoke/WMI implementation; an Avalonia app supplies a
/// cross-platform one. Keeps consumers free of Windows-only dependencies so they can live in
/// <c>Wabbajack.App.Core</c>.
/// </summary>
public interface ISystemParameters
{
    SystemParameters Create();
}
