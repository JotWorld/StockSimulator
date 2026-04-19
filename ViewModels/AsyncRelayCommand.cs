using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace StockExchangeSimulator.ViewModels
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        private readonly Predicate<object?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
            : this(
                _ => executeAsync(),
                canExecute == null ? null : new Predicate<object?>(_ => canExecute()))
        {
        }

        public AsyncRelayCommand(Func<object?, Task> executeAsync, Predicate<object?>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool IsExecuting => _isExecuting;

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting)
                return false;

            return _canExecute?.Invoke(parameter) ?? true;
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _executeAsync(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}