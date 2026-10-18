using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OrbitStudio.Presentation.ViewModels;

namespace OrbitStudio.Presentation.Views;

/// <summary>
/// Einfaches, fest angeordnetes Ebenen-Panel (M3). Andocken, Vorschaubilder und Ziehen zum Umsortieren folgen in M8.
/// </summary>
public partial class LayersPanel : UserControl
{
    public LayersPanel()
    {
        InitializeComponent();
    }

    /// <summary>Doppelklick auf eine Ebene öffnet „Ebeneneigenschaften" (gleicher Befehl wie Ebenen › Umbenennen).</summary>
    private void OnLayerDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(LayerList, source) is ListBoxItem
            && Window.GetWindow(this)?.DataContext is MainViewModel main
            && main.LayerPropertiesCommand.CanExecute(null))
        {
            main.LayerPropertiesCommand.Execute(null);
            e.Handled = true;
        }
    }
}
