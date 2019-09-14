using System;
using System.Windows.Input;

namespace Wabbajack
{
    internal class LambdaCommand : ICommand
    {
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;

        public LambdaCommand(Func<bool> canExecute, Action execute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}