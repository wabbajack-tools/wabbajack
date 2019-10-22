using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class MainWindowVM : ViewModel
    {
        public AppState AppState { get; }

        private ViewModel _ActivePane;
        public ViewModel ActivePane { get => _ActivePane; set => this.RaiseAndSetIfChanged(ref _ActivePane, value); }

        public MainWindowVM(RunMode mode)
        {
            this.AppState = new AppState(mode);
        }
    }
}
