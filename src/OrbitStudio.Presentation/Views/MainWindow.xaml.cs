using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using OrbitStudio.Presentation.ViewModels;

namespace OrbitStudio.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        BindShortcuts(logger);
    }

    public MainViewModel ViewModel => _viewModel;

    /// <summary>Tastenkürzel werden nur hier und nur aus der Registry gebunden – nie in einzelnen Controls.</summary>
    private void BindShortcuts(ILogger logger)
    {
        foreach (var command in _viewModel.Registry.All)
        {
            if (command.Gesture is null)
            {
                continue;
            }

            try
            {
                var gesture = GestureDisplay.ToKeyGesture(command.Gesture);
                if (gesture is not null)
                {
                    InputBindings.Add(new KeyBinding(new CommandAdapter(command, _viewModel.Registry), gesture));
                }
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FormatException)
            {
                logger.LogError(ex, "Ungültiges Tastenkürzel {Gesture} für {Command}", command.Gesture, command.Id);
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.CloseAllDocuments())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
