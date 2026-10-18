using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Layers;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Auswahleintrag für einen Mischmodus mit deutschem Namen.</summary>
public sealed record BlendModeOption(BlendMode Mode, string Name)
{
    public static IReadOnlyList<BlendModeOption> All { get; } =
        Enum.GetValues<BlendMode>().Select(m => new BlendModeOption(m, NameOf(m))).ToList();

    public static string NameOf(BlendMode mode) => Loc.Get("Blend." + mode);

    public override string ToString() => Name;
}

/// <summary>
/// Eine Zeile im Ebenen-Panel. Änderungen an Sichtbarkeit und Sperre laufen über <see cref="LayerOperations"/>,
/// damit das Dokument sie meldet und neu zusammensetzt.
/// </summary>
public sealed class LayerItemViewModel : ObservableObject
{
    private readonly ImageDocument _document;

    public LayerItemViewModel(ImageDocument document, LayerNode node)
    {
        _document = document;
        Node = node;
    }

    public LayerNode Node { get; }

    public string Name => Node.Name;

    public bool IsGroup => Node is LayerGroup;

    /// <summary>Einrückung je Verschachtelungsebene.</summary>
    public Thickness Indent => new(Node.Depth * 14, 0, 0, 0);

    public bool IsVisible
    {
        get => Node.IsVisible;
        set => LayerOperations.SetVisibility(_document, Node, value);
    }

    public bool IsLocked
    {
        get => Node.IsLocked;
        set => LayerOperations.SetLocked(_document, Node, value);
    }

    public bool HasMask => Node is PixelLayer { Mask: not null };

    public string? MaskToolTip => Node is PixelLayer { Mask: { } mask }
        ? Loc.Get(mask.IsEnabled ? "Panel.Layers.MaskOn" : "Panel.Layers.MaskOff")
        : null;

    public double MaskOpacity => Node is PixelLayer { Mask.IsEnabled: false } ? 0.35 : 1.0;

    public string Details => Loc.Format("Panel.Layers.Details", Node.Opacity * 100, BlendModeOption.NameOf(Node.BlendMode));

    /// <summary>Alle Anzeigewerte neu lesen (nach Eigenschaftsänderungen ohne Strukturänderung).</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}
