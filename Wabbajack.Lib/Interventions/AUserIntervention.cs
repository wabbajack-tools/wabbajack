using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public abstract class AUserIntervention : ReactiveObject, IUserIntervention
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public abstract string ShortDescription { get; }
        public abstract string ExtendedDescription { get; }

        private bool _handled;
        public bool Handled { get => _handled; set => this.RaiseAndSetIfChanged(ref _handled, value); }

        public int CpuID { get; } = WorkQueue.AsyncLocalQueue?.CpuId ?? WorkQueue.UnassignedCpuId;

        public abstract void Cancel();
        public ICommand CancelCommand { get; }

        public AUserIntervention()
        {
            CancelCommand = ReactiveCommand.Create(() => Cancel());
        }
    }
}
