using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack
{
    public class ViewModel : ReactiveObject, IDisposable
    {
        private readonly Lazy<CompositeDisposable> _CompositeDisposable = new Lazy<CompositeDisposable>();
        public CompositeDisposable CompositeDisposable => _CompositeDisposable.Value;

        public virtual void Dispose()
        {
            if (_CompositeDisposable.IsValueCreated)
            {
                _CompositeDisposable.Value.Dispose();
            }
        }

        protected void RaiseAndSetIfChanged<T>(
            ref T item,
            T newItem,
            [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(item, newItem)) return;
            item = newItem;
            this.RaisePropertyChanged(propertyName);
        }
    }
}
