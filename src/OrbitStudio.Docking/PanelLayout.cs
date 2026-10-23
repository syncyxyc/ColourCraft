using System.Collections.Immutable;

namespace OrbitStudio.Docking;

/// <summary>Kennungen der Standard-Panels (Spezifikation 5.5). Plugin-Panels folgen mit M10.</summary>
public static class PanelIds
{
    public const string Tools = "tools";
    public const string ToolOptions = "toolOptions";
    public const string Layers = "layers";
    public const string Colors = "colors";
    public const string History = "history";
    public const string Navigator = "navigator";
    public const string Properties = "properties";
    public const string EffectBrowser = "effectBrowser";

    public static IReadOnlyList<string> All { get; } =
        [Tools, ToolOptions, Layers, Colors, History, Navigator, Properties, EffectBrowser];
}

public enum DockSide
{
    Left,
    Right,
    Top,
    Bottom,
}

/// <summary>Zustand eines Panels. <see cref="Size"/> ist die Breite (links/rechts) bzw. Höhe (oben/unten) in geräteunabhängigen Einheiten.</summary>
public sealed record PanelState(string Id, DockSide Side, bool Visible, double Size);

/// <summary>
/// Anordnung der Panels: Reihenfolge der Liste = Reihenfolge innerhalb einer Seite. Unveränderlich; jede Operation liefert
/// ein gültiges Layout (jedes Standard-Panel genau einmal, Größen im erlaubten Bereich).
/// </summary>
public sealed class PanelLayout : IEquatable<PanelLayout>
{
    public const double MinSize = 48;
    public const double MaxSize = 1200;

    private readonly ImmutableArray<PanelState> _panels;

    private PanelLayout(ImmutableArray<PanelState> panels) => _panels = panels;

    public IReadOnlyList<PanelState> Panels => _panels;

    /// <summary>Standardanordnung: Werkzeuge links; Ebenen, Farben, Historie, Navigator rechts; Optionen oben; Rest ausgeblendet.</summary>
    public static PanelLayout Default { get; } = new(
    [
        new PanelState(PanelIds.Tools, DockSide.Left, true, 64),
        new PanelState(PanelIds.ToolOptions, DockSide.Top, true, 120),
        new PanelState(PanelIds.Layers, DockSide.Right, true, 280),
        new PanelState(PanelIds.Colors, DockSide.Right, true, 280),
        new PanelState(PanelIds.History, DockSide.Right, true, 280),
        new PanelState(PanelIds.Navigator, DockSide.Right, true, 280),
        new PanelState(PanelIds.Properties, DockSide.Right, false, 280),
        new PanelState(PanelIds.EffectBrowser, DockSide.Right, false, 280),
    ]);

    public PanelState Get(string id) =>
        _panels.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"Panel '{id}' fehlt.");

    /// <summary>Panels einer Seite in ihrer Reihenfolge (auch ausgeblendete).</summary>
    public IEnumerable<PanelState> OnSide(DockSide side) => _panels.Where(p => p.Side == side);

    public PanelLayout SetVisible(string id, bool visible) => Update(id, p => p with { Visible = visible });

    /// <summary>Ändert die Größe; ungültige Zahlen (NaN, ∞) werden ignoriert, alles andere auf den erlaubten Bereich begrenzt.</summary>
    public PanelLayout Resize(string id, double size) =>
        double.IsFinite(size) ? Update(id, p => p with { Size = Math.Clamp(size, MinSize, MaxSize) }) : this;

    /// <summary>
    /// Verschiebt ein Panel auf eine Seite an die Position <paramref name="index"/> unter den Panels dieser Seite
    /// (wird begrenzt). Die Größe bleibt erhalten.
    /// </summary>
    public PanelLayout Move(string id, DockSide side, int index)
    {
        var panel = Get(id) with { Side = side };
        var rest = _panels.Where(p => p.Id != id).ToList();
        var onSide = rest.Select((p, i) => (p, i)).Where(t => t.p.Side == side).Select(t => t.i).ToList();
        index = Math.Clamp(index, 0, onSide.Count);
        var insertAt = index < onSide.Count ? onSide[index] : onSide.Count > 0 ? onSide[^1] + 1 : rest.Count;
        rest.Insert(insertAt, panel);
        return new PanelLayout([.. rest]);
    }

    /// <summary>Prüft ein Layout; liefert null oder eine deutsche Fehlermeldung.</summary>
    public static string? Validate(IReadOnlyList<PanelState>? panels)
    {
        if (panels is null)
        {
            return "Kein Layout vorhanden.";
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in panels)
        {
            if (!PanelIds.All.Contains(p.Id))
            {
                return $"Unbekanntes Panel '{p.Id}'.";
            }

            if (!ids.Add(p.Id))
            {
                return $"Panel '{p.Id}' kommt mehrfach vor.";
            }

            if (!Enum.IsDefined(p.Side))
            {
                return $"Ungültige Seite bei Panel '{p.Id}'.";
            }

            if (!double.IsFinite(p.Size) || p.Size < MinSize || p.Size > MaxSize)
            {
                return $"Ungültige Größe bei Panel '{p.Id}'.";
            }
        }

        return ids.Count == PanelIds.All.Count ? null : "Es fehlen Panels.";
    }

    /// <summary>
    /// Macht aus beliebigen (z. B. gelesenen) Panel-Angaben ein gültiges Layout: Unbekannte und doppelte Panels entfallen,
    /// fehlende kommen mit Standardwerten ans Ende ihrer Standardseite, ungültige Seiten und Größen werden ersetzt.
    /// <paramref name="changed"/> ist true, wenn dabei etwas geändert werden musste.
    /// </summary>
    public static PanelLayout Normalize(IEnumerable<PanelState?>? panels, out bool changed)
    {
        changed = false;
        var result = new List<PanelState>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in panels ?? [])
        {
            if (p is null || p.Id is null || !PanelIds.All.Contains(p.Id) || !seen.Add(p.Id))
            {
                changed = true;
                continue;
            }

            var fallback = Default.Get(p.Id);
            var side = Enum.IsDefined(p.Side) ? p.Side : fallback.Side;
            var size = double.IsFinite(p.Size) ? Math.Clamp(p.Size, MinSize, MaxSize) : fallback.Size;
            changed |= side != p.Side || size != p.Size;
            result.Add(p with { Side = side, Size = size });
        }

        foreach (var missing in Default.Panels.Where(d => !seen.Contains(d.Id)))
        {
            changed = true;
            result.Add(missing);
        }

        return new PanelLayout([.. result]);
    }

    public static PanelLayout Create(IEnumerable<PanelState> panels)
    {
        var list = panels.ToImmutableArray();
        var error = Validate(list);
        return error is null ? new PanelLayout(list) : throw new ArgumentException(error, nameof(panels));
    }

    private PanelLayout Update(string id, Func<PanelState, PanelState> change)
    {
        var index = _panels.IndexOf(Get(id));
        return new PanelLayout(_panels.SetItem(index, change(_panels[index])));
    }

    public bool Equals(PanelLayout? other) => other is not null && _panels.SequenceEqual(other._panels);

    public override bool Equals(object? obj) => obj is PanelLayout other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_panels.Length, _panels.Length > 0 ? _panels[0] : null);
}
