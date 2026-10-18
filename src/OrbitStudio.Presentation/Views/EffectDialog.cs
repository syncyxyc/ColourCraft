using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Effects;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.Viewport;

namespace OrbitStudio.Presentation.Views;

/// <summary>
/// Effektdialog: je Parameter Schieberegler und Zahlenfeld (bzw. Kontrollkästchen, Farbfeld mit Hex-Feld), Live-Vorschau
/// der aktiven Ebene in reduzierter Auflösung, „Original zeigen", „Zurücksetzen". Jede Änderung bricht die laufende
/// Vorschauberechnung ab und startet eine neue im Hintergrund. Ungültige Eingaben werden begrenzt bzw. abgewiesen;
/// Regeln über mehrere Parameter sperren „OK" mit Meldung.
/// </summary>
public sealed class EffectDialog : Window
{
    public const int PreviewWidth = 360;
    public const int PreviewHeight = 280;

    private readonly IEffect _effect;
    private readonly EffectPreview _preview;
    private readonly List<Action> _refreshers = [];
    private readonly Image _image;
    private readonly System.Windows.Media.Imaging.WriteableBitmap _original;
    private readonly TextBlock _status;
    private readonly TextBlock _error;
    private readonly CheckBox _showOriginal;
    private readonly Button _ok;
    private System.Windows.Media.Imaging.WriteableBitmap? _rendered;
    private CancellationTokenSource? _previewCancellation;
    private EffectSettings _settings;
    private bool _syncing;

    public EffectDialog(EffectDialogRequest request)
    {
        _effect = request.Effect;
        _preview = request.Preview;
        _settings = request.Initial;
        _syncing = true; // Bedienelemente beim Aufbau füllen, ohne Änderungen auszulösen
        Title = Loc.WithoutAccessKey(Loc.Get(_effect.NameKey));
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Brush.Window.Background");
        SetResourceReference(ForegroundProperty, "Brush.Text");

        var root = new Grid { Margin = new Thickness(16) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto), MinWidth = 300 });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Links: Beschreibung und Parameter
        var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0), MaxWidth = 380 };
        left.Children.Add(new TextBlock
        {
            Text = Loc.Get(_effect.DescriptionKey),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Margin = new Thickness(0, 0, 0, 10),
        });
        foreach (var parameter in _effect.Parameters)
        {
            left.Children.Add(parameter switch
            {
                NumberParameter n => NumberRow(n),
                BoolParameter b => BoolRow(b),
                ColorParameter c => ColorRow(c),
                _ => throw new NotSupportedException(parameter.GetType().Name),
            });
        }

        _error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Error");
        AutomationProperties.SetLiveSetting(_error, AutomationLiveSetting.Assertive);
        left.Children.Add(_error);
        root.Children.Add(left);

        // Rechts: Vorschau
        var right = new StackPanel();
        _original = BitmapBridge.Create(_preview.Source);
        _image = new Image { Source = _original, Stretch = Stretch.Uniform, Width = PreviewWidth, Height = PreviewHeight };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        var frame = new Border { BorderThickness = new Thickness(1), Child = _image };
        frame.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        frame.SetResourceReference(Border.BackgroundProperty, "Brush.Checkerboard");
        AutomationProperties.SetName(frame, Loc.Get("Dlg.Effect.Preview"));
        right.Children.Add(frame);
        var previewBar = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        _showOriginal = new CheckBox { Content = Loc.Get("Dlg.Effect.ShowOriginal") };
        _showOriginal.Checked += (_, _) => ShowCurrentImage();
        _showOriginal.Unchecked += (_, _) => ShowCurrentImage();
        previewBar.Children.Add(_showOriginal);
        _status = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, Opacity = 0.75 };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        previewBar.Children.Add(_status);
        right.Children.Add(previewBar);
        if (_preview.Scale < 1)
        {
            right.Children.Add(new TextBlock
            {
                Text = Loc.Format("Dlg.Effect.Reduced", Math.Round(_preview.Scale * 100)),
                Opacity = 0.6,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }

        Grid.SetColumn(right, 1);
        root.Children.Add(right);

        // Schaltflächen
        var buttons = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        var reset = new Button { Content = Loc.Get("Dlg.Effect.Reset"), MinWidth = 100 };
        reset.Click += (_, _) => SetAll(EffectSettings.Defaults(_effect));
        buttons.Children.Add(reset);
        var right2 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _ok = new Button { Content = Loc.Get("Common.Ok"), MinWidth = 88, IsDefault = true };
        _ok.Click += (_, _) =>
        {
            Result = _settings;
            DialogResult = true;
        };
        right2.Children.Add(_ok);
        right2.Children.Add(new Button { Content = Loc.Get("Common.Cancel"), MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        buttons.Children.Add(right2);
        Grid.SetRow(buttons, 1);
        Grid.SetColumnSpan(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
        _syncing = false;
        Closed += (_, _) => _previewCancellation?.Cancel();
        Loaded += (_, _) =>
        {
            Validate();
            SchedulePreview();
        };
    }

    public EffectSettings? Result { get; private set; }

    /// <summary>Aktuelle (noch nicht bestätigte) Einstellungen.</summary>
    public EffectSettings Current => _settings;

    /// <summary>Zuletzt fertig berechnete Vorschau (für Tests und Bildschirmfotos).</summary>
    public PixelBuffer? LastPreview { get; private set; }

    /// <summary>Eine Vorschau wurde fertig berechnet und angezeigt.</summary>
    public event EventHandler? PreviewUpdated;

    /// <summary>Wert setzen wie über das Bedienelement (begrenzt, validiert, Vorschau neu).</summary>
    public void SetValue(string id, object value)
    {
        _settings = _settings.With(_effect, id, value);
        Refresh();
        Validate();
        SchedulePreview();
    }

    private void SetAll(EffectSettings settings)
    {
        _settings = settings;
        Refresh();
        Validate();
        SchedulePreview();
    }

    private void Refresh()
    {
        _syncing = true;
        try
        {
            foreach (var refresh in _refreshers)
            {
                refresh();
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Validate()
    {
        var errors = EffectEngine.Validate(_effect, _settings);
        _ok.IsEnabled = errors.Count == 0;
        _error.Text = string.Join(Environment.NewLine, errors.Select(e => Loc.Get(e.MessageKey)).Distinct());
        _error.Visibility = errors.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SchedulePreview()
    {
        _previewCancellation?.Cancel();
        if (EffectEngine.Validate(_effect, _settings).Count > 0)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        var settings = _settings;
        _status.Text = Loc.Get("Dlg.Effect.Computing");
        var token = cancellation.Token;
        Task.Run(() => _preview.Render(_effect, settings, token), token).ContinueWith(task =>
        {
            if (token.IsCancellationRequested || task.IsCanceled)
            {
                return;
            }

            if (task.Exception is { } error)
            {
                _status.Text = Loc.Format("Dlg.Effect.Failed", error.GetBaseException().Message);
                return;
            }

            LastPreview = task.Result;
            _rendered = BitmapBridge.Create(task.Result);
            _status.Text = string.Empty;
            ShowCurrentImage();
            PreviewUpdated?.Invoke(this, EventArgs.Empty);
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ShowCurrentImage() =>
        _image.Source = _showOriginal.IsChecked == true || _rendered is null ? _original : _rendered;

    // --- Zeilen ------------------------------------------------------------------------------------

    private FrameworkElement NumberRow(NumberParameter p)
    {
        var label = Loc.Get(p.LabelKey);
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

        var slider = new Slider
        {
            Minimum = p.Minimum,
            Maximum = p.Maximum,
            SmallChange = p.Step,
            LargeChange = Math.Max(p.Step, (p.Maximum - p.Minimum) / 20),
            IsSnapToTickEnabled = true,
            TickFrequency = p.Step,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(slider, Loc.WithoutAccessKey(label));
        var box = new TextBox { Margin = new Thickness(6, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(box, Loc.WithoutAccessKey(label));
        var caption = new Label { Content = label, Target = box, Padding = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

        string Format(double v) => v.ToString(p.Decimals == 0 ? "0" : "0." + new string('#', p.Decimals), Loc.Culture);
        void Commit()
        {
            var text = box.Text.Trim().TrimEnd('%', '°').Trim();
            if (double.TryParse(text, NumberStyles.Float, Loc.Culture, out var v)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            {
                SetValue(p.Id, v);
            }

            box.Text = Format(_settings.Number(p.Id)); // begrenzt bzw. ungültige Eingabe verworfen
        }

        slider.ValueChanged += (_, e) =>
        {
            if (!_syncing)
            {
                SetValue(p.Id, e.NewValue);
            }
        };
        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                box.SelectAll();
                e.Handled = true;
            }
        };
        _refreshers.Add(() =>
        {
            slider.Value = _settings.Number(p.Id);
            box.Text = Format(_settings.Number(p.Id));
        });
        _refreshers[^1]();

        Grid.SetColumn(slider, 1);
        Grid.SetColumn(box, 2);
        var unit = new TextBlock { Text = p.UnitKey.Length == 0 ? string.Empty : Loc.Get(p.UnitKey), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(unit, 3);
        grid.Children.Add(caption);
        grid.Children.Add(slider);
        grid.Children.Add(box);
        grid.Children.Add(unit);
        return grid;
    }

    private FrameworkElement BoolRow(BoolParameter p)
    {
        var check = new CheckBox { Content = Loc.Get(p.LabelKey), Margin = new Thickness(0, 4, 0, 4) };
        check.Checked += (_, _) => { if (!_syncing) { SetValue(p.Id, true); } };
        check.Unchecked += (_, _) => { if (!_syncing) { SetValue(p.Id, false); } };
        _refreshers.Add(() => check.IsChecked = _settings.Bool(p.Id));
        _refreshers[^1]();
        return check;
    }

    private FrameworkElement ColorRow(ColorParameter p)
    {
        var label = Loc.Get(p.LabelKey);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        var swatch = new Border();
        var button = new Button
        {
            Width = 32,
            Height = 22,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = swatch,
            ToolTip = Loc.Get("Opt.Color.Pick"),
        };
        AutomationProperties.SetName(button, Loc.WithoutAccessKey(label));
        var hex = new TextBox { Width = 90, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(hex, Loc.WithoutAccessKey(label) + " (Hex)");
        void CommitHex()
        {
            if (ColorRgba.TryParseHex(hex.Text, out var color))
            {
                SetValue(p.Id, color);
            }

            hex.Text = _settings.Color(p.Id).ToHex();
        }

        button.Click += (_, _) =>
        {
            var dialog = new ColorDialog(_settings.Color(p.Id)) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is { } chosen)
            {
                SetValue(p.Id, chosen);
            }
        };
        hex.LostKeyboardFocus += (_, _) => CommitHex();
        hex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitHex();
                e.Handled = true;
            }
        };
        _refreshers.Add(() =>
        {
            var c = _settings.Color(p.Id);
            swatch.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B));
            hex.Text = c.ToHex();
        });
        _refreshers[^1]();

        panel.Children.Add(new Label { Content = label, Target = button, Width = 130, Padding = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(button);
        panel.Children.Add(hex);
        return panel;
    }
}
