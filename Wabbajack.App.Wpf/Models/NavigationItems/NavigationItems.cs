using Wabbajack.Messages;

namespace Wabbajack.Models;

public class HomeNI : ANavigationItem
{
    public override NavigateToGlobal.ScreenType Screen => NavigateToGlobal.ScreenType.Home;
    public override bool MainMenuItem => true;
}
public class ModlistGalleryNI : ANavigationItem
{
    public override NavigateToGlobal.ScreenType Screen => NavigateToGlobal.ScreenType.ModListGallery;
    public override bool MainMenuItem => true;
}
public class CompileNI : ANavigationItem
{
    public override NavigateToGlobal.ScreenType Screen => NavigateToGlobal.ScreenType.Compiler;
    public override bool MainMenuItem => true;
}
public class SettingsNI : ANavigationItem
{
    public override NavigateToGlobal.ScreenType Screen => NavigateToGlobal.ScreenType.Settings;
    public override bool MainMenuItem => true;
}
