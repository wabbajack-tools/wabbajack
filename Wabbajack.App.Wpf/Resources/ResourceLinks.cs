using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Wabbajack
{
    public static class ResourceLinks
    {
        public static Lazy<BitmapImage> WabbajackLogo { get; } = new Lazy<BitmapImage>(() =>
            UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Wabba_Mouth.png")).Stream));
        public static Lazy<BitmapImage> WabbajackLogoNoText { get; } = new Lazy<BitmapImage>(() =>
            UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Wabba_Mouth_No_Text.png")).Stream));
        public static Lazy<BitmapImage> WabbajackErrLogo { get; } = new Lazy<BitmapImage>(() =>
            UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Wabba_Ded.png")).Stream));
        public static Lazy<BitmapImage> MO2Button { get; } = new Lazy<BitmapImage>(() =>
            UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/MO2Button.png")).Stream));
        public static Lazy<BitmapImage> MiddleMouseButton { get; } = new Lazy<BitmapImage>(() =>
            UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/middle_mouse_button.png")).Stream));
        public static Lazy<BitmapImage> MegaLogo { get; } = new Lazy<BitmapImage>(() =>
            UIUtils.BitmapImageFromStream(Application.GetResourceStream(new Uri("pack://application:,,,/LoginManagers/Icons/mega.png")).Stream));
    }
}
