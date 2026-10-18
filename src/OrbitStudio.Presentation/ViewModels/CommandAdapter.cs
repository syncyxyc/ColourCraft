using System.Windows.Input;
using OrbitStudio.Application.Commands;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Verbindet einen Registry-Befehl mit dem WPF-<see cref="ICommand"/>-Modell.</summary>
public sealed class CommandAdapter : ICommand
{
    private readonly AppCommand _command;

    public CommandAdapter(AppCommand command, CommandRegistry registry)
    {
        _command = command;
        registry.RequerySuggested += (_, _) => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CanExecuteChanged;

    public AppCommand Command => _command;

    public bool CanExecute(object? parameter) => _command.CanExecute();

    public void Execute(object? parameter) => _command.Execute();
}
