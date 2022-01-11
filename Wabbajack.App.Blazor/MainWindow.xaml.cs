using System;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace Wabbajack.App.Blazor
{
    public partial class MainWindow
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly IStore              _store;

        public MainWindow(ILogger<MainWindow> logger, IStore store, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _store  = store;
            _store.InitializeAsync().Wait();
            Resources.Add("services", serviceProvider);
            InitializeComponent();
        }
    }

    // Required so compiler doesn't complain about not finding the type. [MC3050]
    public partial class Main { }
}