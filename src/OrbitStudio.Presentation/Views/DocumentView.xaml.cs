using System.Windows.Controls;
using System.Windows.Input;
using OrbitStudio.Presentation.ViewModels;

namespace OrbitStudio.Presentation.Views;

public partial class DocumentView : UserControl
{
    public DocumentView()
    {
        InitializeComponent();
    }

    private void OnPageMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is DocumentViewModel vm)
        {
            var p = e.GetPosition(Page);
            vm.UpdateCursor(p.X, p.Y);
        }
    }

    private void OnPageMouseLeave(object sender, MouseEventArgs e)
    {
        (DataContext as DocumentViewModel)?.ClearCursor();
    }
}
