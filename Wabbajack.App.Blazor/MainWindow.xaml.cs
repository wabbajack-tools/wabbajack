using System;
using Microsoft.Extensions.Logging;

namespace Wabbajack.App.Blazor
{
    public partial class MainWindow
    {
        private readonly ILogger<MainWindow> _logger;
        public MainWindow(ILogger<MainWindow> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            Resources.Add("services", serviceProvider);
            InitializeComponent();
        }
    }
    
    // Required so compiler doesn't complain about not finding the type. [MC3050]
    public partial class Main {}
}
