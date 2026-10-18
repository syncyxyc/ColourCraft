using System.Globalization;
using System.Windows;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Presentation.Services;

namespace OrbitStudio.Presentation.Views;

public partial class NewDocumentDialog : Window
{
    public NewDocumentDialog(string defaultName, int defaultWidth, int defaultHeight)
    {
        InitializeComponent();
        NameBox.Text = defaultName;
        WidthBox.Text = defaultWidth.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = defaultHeight.ToString(CultureInfo.InvariantCulture);
        NameBox.SelectAll();
    }

    public NewDocumentRequest? Result { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var background = TransparentOption.IsChecked == true ? DocumentBackground.Transparent : DocumentBackground.White;
        var (request, error) = NewDocumentValidator.Validate(NameBox.Text, WidthBox.Text, HeightBox.Text, background);
        if (request is null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = request;
        DialogResult = true;
    }
}
