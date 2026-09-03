using System;
using System.Windows.Input;

namespace ImplantMaterialID.ViewModels
{
    /// <summary>
    /// Simple synchronous ICommand implementation. Command handlers that need to await
    /// asynchronous work pass an `async () => await ...` lambda as the execute delegate -
    /// acceptable here since these are UI-triggered, fire-and-forget-from-the-command's-
    /// perspective operations, and the ViewModel methods themselves handle their own
    /// try/catch/finally around IsBusy state.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        /// <summary>Forces WPF to re-query CanExecute for this command right now.</summary>
        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
