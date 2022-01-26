using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;

namespace Wabbajack.Networking.Browser.ViewModels
{
    public class ViewModelBase : ReactiveObject, IActivatableViewModel
    {
        public ViewModelBase()
        {
            Activator = new ViewModelActivator();
        }

        public ViewModelActivator Activator { get; }
    }
}
