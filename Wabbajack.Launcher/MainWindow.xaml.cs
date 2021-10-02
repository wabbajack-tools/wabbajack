using System;
using Avalonia.Controls;

namespace Wabbajack.Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                DataContext = new MainWindowVM();
            }
            catch(Exception ex)
            {
                System.Console.Error.WriteLine("Error creating datacontext.");
                System.Console.Error.WriteLine(ex);
                throw;
            }
        }
    }
}
