using System;
using Wabbajack.App.Interfaces;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Views
{
    public abstract class ScreenBase<T> : ViewBase<T>, IScreenView
    where T : ViewModelBase
    {
        protected ScreenBase(bool createViewModel = true) : base(createViewModel)
        {

        }

        public Type ViewModelType => typeof(T);
    }
}