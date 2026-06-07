using System.Windows.Input;

namespace Plants.Helpers;

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(ConvertParameter(parameter)) ?? true;

    public void Execute(object? parameter) => _execute(ConvertParameter(parameter));

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? ConvertParameter(object? parameter)
    {
        if (parameter is T value)
        {
            return value;
        }

        return default;
    }
}
