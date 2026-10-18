using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbitStudio.Application.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.ViewModels;
using OrbitStudio.Tools;

namespace OrbitStudio.Presentation.Tooling;

/// <summary>
/// Verwaltet Werkzeuge, aktives Werkzeug, Farben und den Masken-Bearbeitungsmodus und leitet Eingaben der Leinwand
/// an das Werkzeug weiter. Werkzeuge sind global, wirken aber immer auf das Dokument der jeweiligen Leinwand.
/// </summary>
public sealed partial class ToolController : ObservableObject
{
    private readonly HistoryService _histories;
    private readonly IDialogService _dialogs;
    private DocumentViewModel? _operationDocument;

    public ToolController(HistoryService histories, IDialogService dialogs)
    {
        _histories = histories;
        _dialogs = dialogs;
        Tools = ToolSet.CreateDefault();
        Items = Tools.Select(t => new ToolItemViewModel(t, this)).ToList();
        ActiveTool = Tools.First(t => t.Id == "brush");
        foreach (var tool in Tools)
        {
            tool.OverlayChanged += (s, _) =>
            {
                if (s == ActiveTool)
                {
                    OverlayChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        Colors.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(PrimaryColor));
            OnPropertyChanged(nameof(SecondaryColor));
        };
        SelectToolCommand = new RelayCommand<ITool>(t => { if (t is not null) { ActiveTool = t; } });
    }

    public IReadOnlyList<ITool> Tools { get; }

    public IReadOnlyList<ToolItemViewModel> Items { get; }

    public ToolColors Colors { get; } = new();

    public ICommand SelectToolCommand { get; }

    /// <summary>Optionen des aktiven Werkzeugs für die Optionsleiste.</summary>
    public ObservableCollection<object> Options { get; } = [];

    public System.Windows.Media.Color PrimaryColor => ToMedia(Colors.Primary);

    public System.Windows.Media.Color SecondaryColor => ToMedia(Colors.Secondary);

    public string ActiveToolName => Loc.Get(ActiveTool.NameKey);

    public string? ActiveToolHint => ActiveTool.Id switch
    {
        "crop" => Loc.Get("Tool.Hint.crop"),
        "selectPolygon" => Loc.Get("Tool.Hint.selectPolygon") + " · " + Loc.Get("Tool.Hint.selection"),
        "move" => Loc.Get("Tool.Hint.move"),
        "dashPattern" => Loc.Get("Tool.Hint.dashPattern"),
        _ when ActiveTool.Category == ToolCategory.Selection => Loc.Get("Tool.Hint.selection"),
        _ => null,
    };

    /// <summary>Malwerkzeuge bearbeiten die Maske der aktiven Ebene.</summary>
    [ObservableProperty]
    private bool _editMask;

    [ObservableProperty]
    private ITool _activeTool = null!; // wird im Konstruktor gesetzt

    /// <summary>Überlagerungen des aktiven Werkzeugs haben sich geändert.</summary>
    public event EventHandler? OverlayChanged;

    /// <summary>Ein Werkzeug konnte eine Eingabe nicht ausführen.</summary>
    public event EventHandler<ToolProblem>? ProblemReported;

    partial void OnActiveToolChanging(ITool value)
    {
        CommitActive();
    }

    partial void OnActiveToolChanged(ITool value)
    {
        foreach (var item in Items)
        {
            item.Refresh();
        }

        RebuildOptions();
        OnPropertyChanged(nameof(ActiveToolName));
        OnPropertyChanged(nameof(ActiveToolHint));
        OverlayChanged?.Invoke(this, EventArgs.Empty);
    }

    public ToolContext ContextFor(DocumentViewModel document)
    {
        var context = new ToolContext(document.Document, _histories.For(document.Document), Colors, document.View)
        {
            EditMask = EditMask,
        };
        context.Problem += (_, problem) => ProblemReported?.Invoke(this, problem);
        return context;
    }

    public void PointerDown(DocumentViewModel document, PointD position, PointD viewport, PointerButton button,
        ToolModifiers modifiers, int clickCount)
    {
        if (_operationDocument is not null && _operationDocument != document)
        {
            CommitActive();
        }

        _operationDocument = document;
        ActiveTool.PointerDown(ContextFor(document), new ToolPointerEvent(position, viewport, button, modifiers, clickCount));
    }

    public void PointerMove(DocumentViewModel document, PointD position, PointD viewport, PointerButton button,
        ToolModifiers modifiers) =>
        ActiveTool.PointerMove(ContextFor(document), new ToolPointerEvent(position, viewport, button, modifiers));

    public void PointerUp(DocumentViewModel document, PointD position, PointD viewport, PointerButton button,
        ToolModifiers modifiers) =>
        ActiveTool.PointerUp(ContextFor(document), new ToolPointerEvent(position, viewport, button, modifiers));

    public void Hover(DocumentViewModel document, PointD position) => ActiveTool.Hover(ContextFor(document), position);

    /// <summary>Ziehen endete ohne Loslassen (Mausfang verloren).</summary>
    public void PointerLost(DocumentViewModel document)
    {
        if (_operationDocument == document && ActiveTool.IsActiveOperation)
        {
            ActiveTool.PointerLost(ContextFor(document));
        }
    }

    /// <summary>Zoom oder Verschiebung eines Dokuments haben sich geändert.</summary>
    public void ViewChanged(DocumentViewModel document) => ActiveTool.ViewChanged(ContextFor(document));

    /// <summary>Schaltfläche der Optionsleiste ausführen (im Kontext des zuletzt bearbeiteten Dokuments).</summary>
    public void ExecuteAction(ActionOption action) =>
        action.Execute(_operationDocument is { } document ? ContextFor(document) : null);

    /// <summary>Farbwahl für eine Farboption über den Farbdialog.</summary>
    public void PickColor(ColorOption option)
    {
        if (_dialogs.ShowColorPicker(option.Value) is { } chosen)
        {
            option.Value = chosen;
        }
    }

    public bool Key(DocumentViewModel document, ToolKey key, ToolModifiers modifiers)
    {
        _operationDocument = document;
        return ActiveTool.Key(ContextFor(document), key, modifiers);
    }

    /// <summary>Laufende Werkzeugbearbeitung übernehmen (vor Rückgängig, Werkzeug- oder Dokumentwechsel).</summary>
    public void CommitActive()
    {
        if (_operationDocument is { } document && ActiveTool.IsActiveOperation)
        {
            ActiveTool.Commit(ContextFor(document));
        }
    }

    public void CancelActive()
    {
        if (_operationDocument is { } document && ActiveTool.IsActiveOperation)
        {
            ActiveTool.Cancel(ContextFor(document));
        }
    }

    /// <summary>Dokument wird geschlossen: laufende Bearbeitung verwerfen.</summary>
    public void ForgetDocument(DocumentViewModel document)
    {
        if (_operationDocument == document)
        {
            CancelActive();
            _operationDocument = null;
        }
    }

    public ITool? FindTool(string id) => Tools.FirstOrDefault(t => t.Id == id);

    private void RebuildOptions()
    {
        foreach (var old in Options.OfType<IDisposable>())
        {
            old.Dispose();
        }

        Options.Clear();
        foreach (var option in ActiveTool.Options)
        {
            Options.Add(option switch
            {
                NumberOption n => new NumberOptionViewModel(n),
                ToggleOption t => new ToggleOptionViewModel(t),
                ChoiceOption c => new ChoiceOptionViewModel(c),
                ColorOption c => new ColorOptionViewModel(c, this),
                ActionOption a => new ActionOptionViewModel(a, this),
                InfoOption i => new InfoOptionViewModel(i),
                _ => throw new NotSupportedException(option.GetType().Name),
            });
        }
    }

    private static System.Windows.Media.Color ToMedia(ColorRgba c) => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
}

/// <summary>Eintrag der Werkzeugleiste.</summary>
public sealed class ToolItemViewModel(ITool tool, ToolController controller) : ObservableObject
{
    public ITool Tool { get; } = tool;

    public string Glyph => Tool.Glyph;

    public string Name => Loc.Get(Tool.NameKey);

    public string ToolTip => Tool.Shortcut is null
        ? Name
        : Loc.Format("Tool.ToolTip", Name, GestureDisplay.ToDisplayText(Tool.Shortcut));

    public bool IsActive
    {
        get => controller.ActiveTool == Tool;
        set
        {
            if (value)
            {
                controller.ActiveTool = Tool;
            }

            OnPropertyChanged();
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsActive));
}

/// <summary>
/// Gemeinsame Basis der Options-ViewModels: folgt Wert- und Freigabeänderungen der Option (auch wenn das Werkzeug sie
/// selbst ändert, z. B. Koordinaten beim Ziehen) und meldet sich beim Neuaufbau der Optionsleiste wieder ab.
/// </summary>
public abstract class OptionViewModel : ObservableObject, IDisposable
{
    private readonly ToolOption _option;

    protected OptionViewModel(ToolOption option)
    {
        _option = option;
        option.Changed += OnOptionChanged;
        option.EnabledChanged += OnEnabledChanged;
    }

    public string Label => Loc.Get(_option.LabelKey);

    public bool IsEnabled => _option.IsEnabled;

    public void Dispose()
    {
        _option.Changed -= OnOptionChanged;
        _option.EnabledChanged -= OnEnabledChanged;
        GC.SuppressFinalize(this);
    }

    /// <summary>Wert der Option hat sich geändert: alle abhängigen Eigenschaften neu melden.</summary>
    protected virtual void OnOptionChanged(object? sender, EventArgs e) => OnPropertyChanged(string.Empty);

    private void OnEnabledChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(IsEnabled));
}

public sealed class NumberOptionViewModel(NumberOption option) : OptionViewModel(option)
{
    public string Unit => Loc.Get(option.UnitKey);

    public double Minimum => option.Minimum;

    public double Maximum => option.Maximum;

    public double Step => option.Step;

    public bool ShowSlider => option.ShowSlider;

    /// <summary>Ungültige Eingaben werden auf den gültigen Bereich begrenzt; das Feld zeigt danach den echten Wert.</summary>
    public double Value
    {
        get => option.Value;
        set
        {
            option.Value = value;
            OnPropertyChanged();
        }
    }
}

public sealed class ToggleOptionViewModel(ToggleOption option) : OptionViewModel(option)
{
    public bool Value
    {
        get => option.Value;
        set
        {
            option.Value = value;
            OnPropertyChanged();
        }
    }
}

public sealed class ChoiceOptionViewModel(ChoiceOption option) : OptionViewModel(option)
{
    public IReadOnlyList<string> Choices { get; } = option.ChoiceKeys.Select(Loc.Get).ToList();

    public int Index
    {
        get => option.Index;
        set
        {
            option.Index = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>Farbe mit Alpha: Farbfeld (öffnet den Farbdialog) und Hex-Feld (#RRGGBB oder #RRGGBBAA).</summary>
public sealed class ColorOptionViewModel : OptionViewModel
{
    private readonly ColorOption _option;

    public ColorOptionViewModel(ColorOption option, ToolController controller)
        : base(option)
    {
        _option = option;
        PickCommand = new RelayCommand(() => controller.PickColor(option));
    }

    public ICommand PickCommand { get; }

    public System.Windows.Media.Color Color =>
        System.Windows.Media.Color.FromArgb(_option.Value.A, _option.Value.R, _option.Value.G, _option.Value.B);

    /// <summary>Ungültige Hex-Eingaben werden abgewiesen; das Feld zeigt danach wieder die gültige Farbe.</summary>
    public string Hex
    {
        get => _option.Value.ToHex();
        set
        {
            if (ColorRgba.TryParseHex(value, out var color))
            {
                _option.Value = color;
            }

            OnPropertyChanged();
        }
    }
}

/// <summary>Schaltfläche der Optionsleiste.</summary>
public sealed class ActionOptionViewModel : OptionViewModel
{
    public ActionOptionViewModel(ActionOption option, ToolController controller)
        : base(option)
    {
        Command = new RelayCommand(() => controller.ExecuteAction(option));
    }

    public ICommand Command { get; }
}

/// <summary>Nur-Lese-Anzeige (z. B. Länge und Winkel der Strichmusterlinie).</summary>
public sealed class InfoOptionViewModel(InfoOption option) : OptionViewModel(option)
{
    public string Text => Loc.Format(option.TextKey, [.. option.Arguments]);
}
