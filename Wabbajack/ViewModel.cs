using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
