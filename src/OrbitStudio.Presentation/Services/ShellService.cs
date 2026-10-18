using System.Windows;

namespace OrbitStudio.Presentation.Services;

/// <summary>Vollbild und Beenden für das Hauptfenster. Das Fenster wird nach dem Erzeugen angehängt.</summary>
public sealed class ShellService : IShellService
{
    private Window? _window;
    private WindowState _previousState;
    private WindowStyle _previousStyle;
    private ResizeMode _previousResizeMode;

    public bool IsFullScreen { get; private set; }

    /// <summary>Fensterzustand ohne Vollbild – wird beim Beenden gespeichert.</summary>
    public WindowState StateOutsideFullScreen => IsFullScreen ? _previousState : _window?.WindowState ?? WindowState.Normal;

    public void Attach(Window window) => _window = window;

    public void ToggleFullScreen()
    {
        if (_window is null)
        {
            return;
        }

        if (!IsFullScreen)
        {
            _previousState = _window.WindowState;
            _previousStyle = _window.WindowStyle;
            _previousResizeMode = _window.ResizeMode;

            // Erst Normal, dann Maximiert: nur so überdeckt ein rahmenloses Fenster auch die Taskleiste.
            _window.WindowState = WindowState.Normal;
            _window.WindowStyle = WindowStyle.None;
            _window.ResizeMode = ResizeMode.NoResize;
            _window.WindowState = WindowState.Maximized;
            IsFullScreen = true;
        }
        else
        {
            _window.WindowStyle = _previousStyle;
            _window.ResizeMode = _previousResizeMode;
            _window.WindowState = _previousState;
            IsFullScreen = false;
        }
    }

    public void RequestExit() => _window?.Close();

    public void SetBusy(bool busy) => System.Windows.Input.Mouse.OverrideCursor = busy ? System.Windows.Input.Cursors.Wait : null;
}
