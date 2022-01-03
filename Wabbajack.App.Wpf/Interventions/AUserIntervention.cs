using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;

namespace Wabbajack
{
    public abstract class AUserIntervention : ReactiveObject, IUserIntervention
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public abstract string ShortDescription { get; }
        public abstract string ExtendedDescription { get; }

        private bool _handled;
        public bool Handled { get => _handled; set => this.RaiseAndSetIfChanged(ref _handled, value); }
        public CancellationToken Token { get; }
        public void SetException(Exception exception)
        {
            throw new NotImplementedException();
        }

        public abstract void Cancel();
        public ICommand CancelCommand { get; }

        public AUserIntervention()
        {
            CancelCommand = ReactiveCommand.Create(() => Cancel());
        }
    }
}
