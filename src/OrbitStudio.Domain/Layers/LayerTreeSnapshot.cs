using OrbitStudio.Domain.Documents;

namespace OrbitStudio.Domain.Layers;

/// <summary>
/// Unveränderlicher Schnappschuss von Struktur und Eigenschaften des Ebenenbaums (ohne Pixelkopien).
/// Ebenen werden per Referenz festgehalten – entfernte Ebenen bleiben dadurch für Rückgängig erhalten.
/// Pixel- und Maskeninhalte, die eine Operation verändert, sichert die Historie zusätzlich.
/// </summary>
public sealed class LayerTreeSnapshot
{
    private readonly List<(LayerGroup Group, LayerNode[] Children)> _structure = [];
    private readonly List<NodeState> _states = [];
    private readonly LayerNode? _active;

    private LayerTreeSnapshot(ImageDocument document)
    {
        Capture(document.Root);
        _active = document.ActiveLayer;
    }

    private readonly record struct NodeState(LayerNode Node, string Name, bool Visible, double Opacity, BlendMode Mode,
        bool Locked, LayerMask? Mask, bool MaskEnabled);

    public static LayerTreeSnapshot Capture(ImageDocument document) => new(document);

    /// <summary>Stellt Struktur, Eigenschaften und aktive Ebene wieder her und meldet die Änderung.</summary>
    public void Restore(ImageDocument document)
    {
        // Zwei Phasen: erst alle betroffenen Gruppen leeren, dann füllen – sonst könnte eine später behandelte Gruppe
        // die Eltern-Verweise einer bereits wiederhergestellten wieder löschen.
        foreach (var (group, _) in _structure)
        {
            group.DetachAllChildren();
        }

        foreach (var (group, children) in _structure)
        {
            group.AttachChildren(children);
        }

        foreach (var s in _states)
        {
            s.Node.Name = s.Name;
            s.Node.IsVisible = s.Visible;
            s.Node.Opacity = s.Opacity;
            s.Node.BlendMode = s.Mode;
            s.Node.IsLocked = s.Locked;
            if (s.Node is PixelLayer layer)
            {
                layer.Mask = s.Mask;
                if (s.Mask is not null)
                {
                    s.Mask.IsEnabled = s.MaskEnabled;
                }
            }
        }

        document.ActiveLayer = _active is not null && document.Contains(_active) ? _active : null;
        document.CommitLayerChange();
    }

    private void Capture(LayerGroup group)
    {
        _structure.Add((group, group.Children.ToArray()));
        foreach (var child in group.Children)
        {
            var layer = child as PixelLayer;
            _states.Add(new NodeState(child, child.Name, child.IsVisible, child.Opacity, child.BlendMode, child.IsLocked,
                layer?.Mask, layer?.Mask?.IsEnabled ?? false));
            if (child is LayerGroup nested)
            {
                Capture(nested);
            }
        }
    }
}
