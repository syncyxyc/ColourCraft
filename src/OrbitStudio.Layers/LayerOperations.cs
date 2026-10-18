using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Rendering;

namespace OrbitStudio.Layers;

/// <summary>Gründe, aus denen eine Ebenenoperation abgelehnt wird. Die Oberfläche zeigt dazu eine deutsche Meldung.</summary>
public enum LayerOperationError
{
    Locked,
    LastPixelLayer,
    NotPixelLayer,
    NotGroup,
    NoLayerBelow,
    LayerBelowNotPixelLayer,
    NoMask,
    AtTop,
    AtBottom,
    NotInDocument,
}

public sealed class LayerOperationException(LayerOperationError error)
    : InvalidOperationException($"Ebenenoperation nicht möglich: {error}")
{
    public LayerOperationError Error { get; } = error;
}

/// <summary>
/// Alle Änderungen am Ebenenbaum. Jede Operation prüft ihre Vorbedingungen, ändert das Modell und meldet
/// die Änderung über <see cref="ImageDocument.CommitLayerChange"/>. Sichtbare Namen kommen vom Aufrufer.
/// Ab M4 werden diese Operationen in rückgängig machbare Historieneinträge gekapselt.
/// </summary>
public static class LayerOperations
{
    /// <summary>Neue transparente Ebene über der aktiven (bzw. oben, wenn nichts aktiv ist). Wird aktiv.</summary>
    public static PixelLayer AddLayer(ImageDocument document, string name)
    {
        var layer = new PixelLayer(name, PixelBuffer.Create(document.Width, document.Height));
        InsertAboveActive(document, layer);
        return layer;
    }

    /// <summary>
    /// Fügt ein Bild als neue Ebene über der aktiven ein, oben links ausgerichtet.
    /// Liefert true in <c>Cropped</c>, wenn das Bild größer als das Dokument war und beschnitten wurde.
    /// </summary>
    public static (PixelLayer Layer, bool Cropped) ImportAsLayer(ImageDocument document, PixelBuffer image, string name,
        int offsetX = 0, int offsetY = 0)
    {
        var pixels = PixelBuffer.Create(document.Width, document.Height);
        var target = new IntRect(offsetX, offsetY, image.Width, image.Height).Intersect(document.Bounds);
        for (var y = target.Y; y < target.Bottom; y++)
        {
            image.GetRow(y - offsetY).Slice((target.X - offsetX) * 4, target.Width * 4)
                .CopyTo(pixels.GetRow(y).Slice(target.X * 4));
        }

        var layer = new PixelLayer(name, pixels);
        InsertAboveActive(document, layer);
        return (layer, target.Width < image.Width || target.Height < image.Height);
    }

    /// <summary>Tiefe Kopie direkt über dem Original; die Kopie wird aktiv und ist nicht gesperrt.</summary>
    public static LayerNode Duplicate(ImageDocument document, LayerNode node, string name)
    {
        EnsureInDocument(document, node);
        var parent = node.Parent!;
        var copy = node.Duplicate(name);
        parent.Insert(parent.IndexOf(node) + 1, copy);
        document.ActiveLayer = copy;
        document.CommitLayerChange();
        return copy;
    }

    public static bool CanDelete(ImageDocument document, LayerNode node) =>
        document.Contains(node) && node != document.Root && !node.IsEffectivelyLocked
        && document.PixelLayers.Except(PixelLayersOf(node)).Any();

    /// <summary>Löscht eine Ebene oder Gruppe samt Inhalt. Mindestens eine Pixelebene bleibt immer erhalten.</summary>
    public static void Delete(ImageDocument document, LayerNode node)
    {
        EnsureInDocument(document, node);
        EnsureUnlocked(node);
        if (!document.PixelLayers.Except(PixelLayersOf(node)).Any())
        {
            throw new LayerOperationException(LayerOperationError.LastPixelLayer);
        }

        var parent = node.Parent!;
        var index = parent.IndexOf(node);
        parent.Remove(node);

        // Nachfolger: Ebene darunter, sonst darüber, sonst die Gruppe selbst, sonst die oberste Ebene.
        LayerNode? next = index > 0 ? parent.Children[index - 1]
            : parent.Children.Count > 0 ? parent.Children[0]
            : parent != document.Root ? parent
            : document.Root.Children.LastOrDefault();
        document.ActiveLayer = next;
        document.CommitLayerChange();
    }

    public static void Rename(ImageDocument document, LayerNode node, string name)
    {
        EnsureInDocument(document, node);
        if (node.Name != name.Trim())
        {
            node.Name = name;
            document.CommitLayerChange();
        }
    }

    public static bool CanMoveUp(LayerNode node) =>
        node.Parent is { } p && p.IndexOf(node) < p.Children.Count - 1 && !node.IsEffectivelyLocked;

    public static bool CanMoveDown(LayerNode node) =>
        node.Parent is { } p && p.IndexOf(node) > 0 && !node.IsEffectivelyLocked;

    /// <summary>Eine Position nach oben innerhalb der eigenen Gruppe.</summary>
    public static void MoveUp(ImageDocument document, LayerNode node)
    {
        EnsureInDocument(document, node);
        EnsureUnlocked(node);
        var parent = node.Parent!;
        var index = parent.IndexOf(node);
        if (index >= parent.Children.Count - 1)
        {
            throw new LayerOperationException(LayerOperationError.AtTop);
        }

        parent.Move(node, index + 1);
        document.CommitLayerChange();
    }

    /// <summary>Eine Position nach unten innerhalb der eigenen Gruppe.</summary>
    public static void MoveDown(ImageDocument document, LayerNode node)
    {
        EnsureInDocument(document, node);
        EnsureUnlocked(node);
        var parent = node.Parent!;
        var index = parent.IndexOf(node);
        if (index <= 0)
        {
            throw new LayerOperationException(LayerOperationError.AtBottom);
        }

        parent.Move(node, index - 1);
        document.CommitLayerChange();
    }

    public static void SetVisibility(ImageDocument document, LayerNode node, bool visible)
    {
        EnsureInDocument(document, node);
        if (node.IsVisible != visible)
        {
            node.IsVisible = visible;
            document.CommitLayerChange();
        }
    }

    public static void SetOpacity(ImageDocument document, LayerNode node, double opacity)
    {
        EnsureInDocument(document, node);
        var clamped = Math.Clamp(opacity, 0, 1);
        if (Math.Abs(node.Opacity - clamped) > 1e-12)
        {
            node.Opacity = clamped;
            document.CommitLayerChange();
        }
    }

    public static void SetBlendMode(ImageDocument document, LayerNode node, BlendMode mode)
    {
        EnsureInDocument(document, node);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (node.BlendMode != mode)
        {
            node.BlendMode = mode;
            document.CommitLayerChange();
        }
    }

    public static void SetLocked(ImageDocument document, LayerNode node, bool locked)
    {
        EnsureInDocument(document, node);
        if (node.IsLocked != locked)
        {
            node.IsLocked = locked;
            document.CommitLayerChange();
        }
    }

    /// <summary>Packt die Ebene in eine neue Gruppe an derselben Position. Die Gruppe wird aktiv.</summary>
    public static LayerGroup CreateGroup(ImageDocument document, LayerNode node, string groupName)
    {
        EnsureInDocument(document, node);
        EnsureUnlocked(node);
        var parent = node.Parent!;
        var index = parent.IndexOf(node);
        var group = new LayerGroup(groupName);
        parent.Remove(node);
        group.Add(node);
        parent.Insert(index, group);
        document.ActiveLayer = group;
        document.CommitLayerChange();
        return group;
    }

    /// <summary>
    /// Löst eine Gruppe auf: Die Kinder rücken in Reihenfolge an ihre Stelle. Deckkraft und Mischmodus der Gruppe
    /// entfallen dabei (das Aussehen kann sich ändern).
    /// </summary>
    public static void DissolveGroup(ImageDocument document, LayerNode node)
    {
        EnsureInDocument(document, node);
        if (node is not LayerGroup group)
        {
            throw new LayerOperationException(LayerOperationError.NotGroup);
        }

        EnsureUnlocked(group);
        var parent = group.Parent!;
        var index = parent.IndexOf(group);
        var children = group.Children.ToList();
        parent.Remove(group);
        foreach (var child in children)
        {
            group.Remove(child);
            parent.Insert(index++, child);
        }

        document.ActiveLayer = children.LastOrDefault() ?? parent.Children.LastOrDefault();
        document.CommitLayerChange();
    }

    /// <summary>Fügt eine weiße Maske (alles sichtbar) hinzu, falls noch keine vorhanden ist.</summary>
    public static LayerMask AddMask(ImageDocument document, LayerNode node)
    {
        var layer = RequirePixelLayer(document, node);
        if (layer.Mask is { } existing)
        {
            return existing;
        }

        layer.Mask = LayerMask.CreateRevealAll(document.Width, document.Height);
        document.CommitLayerChange();
        return layer.Mask;
    }

    public static void RemoveMask(ImageDocument document, LayerNode node)
    {
        var layer = RequirePixelLayer(document, node);
        if (layer.Mask is null)
        {
            throw new LayerOperationException(LayerOperationError.NoMask);
        }

        layer.Mask = null;
        document.CommitLayerChange();
    }

    public static void SetMaskEnabled(ImageDocument document, LayerNode node, bool enabled)
    {
        var mask = RequireMask(document, node);
        if (mask.IsEnabled != enabled)
        {
            mask.IsEnabled = enabled;
            document.CommitLayerChange();
        }
    }

    public static void InvertMask(ImageDocument document, LayerNode node)
    {
        var mask = RequireMask(document, node);
        mask.Invert();
        document.CommitLayerChange();
    }

    /// <summary>Rechnet die (aktive) Maske in die Pixel ein und entfernt sie. Das Aussehen bleibt gleich.</summary>
    public static void ApplyMask(ImageDocument document, LayerNode node)
    {
        var layer = RequirePixelLayer(document, node);
        var mask = layer.Mask ?? throw new LayerOperationException(LayerOperationError.NoMask);
        EnsureUnlocked(layer);
        if (mask.IsEnabled)
        {
            BakeMask(layer.Pixels, mask);
        }

        layer.Mask = null;
        document.CommitLayerChange();
    }

    public static bool CanMergeDown(ImageDocument document, LayerNode node) =>
        node is PixelLayer && document.Contains(node) && node.Parent is { } parent && parent.IndexOf(node) > 0
        && parent.Children[parent.IndexOf(node) - 1] is PixelLayer below
        && !node.IsEffectivelyLocked && !below.IsEffectivelyLocked;

    /// <summary>
    /// Führt die Ebene mit der direkt darunterliegenden Pixelebene zusammen: Die obere wird mit ihrer Deckkraft,
    /// Maske und ihrem Mischmodus auf die Pixel der unteren gemischt und entfernt. Eine Maske der unteren Ebene wird
    /// vorher eingerechnet. Unsichtbare obere Ebenen tragen nichts bei.
    /// </summary>
    public static PixelLayer MergeDown(ImageDocument document, LayerNode node)
    {
        var upper = RequirePixelLayer(document, node);
        var parent = upper.Parent!;
        var index = parent.IndexOf(upper);
        if (index == 0)
        {
            throw new LayerOperationException(LayerOperationError.NoLayerBelow);
        }

        if (parent.Children[index - 1] is not PixelLayer lower)
        {
            throw new LayerOperationException(LayerOperationError.LayerBelowNotPixelLayer);
        }

        EnsureUnlocked(upper);
        EnsureUnlocked(lower);

        if (lower.Mask is { } lowerMask)
        {
            if (lowerMask.IsEnabled)
            {
                BakeMask(lower.Pixels, lowerMask);
            }

            lower.Mask = null;
        }

        if (upper.IsVisible)
        {
            LayerCompositor.BlendInto(lower.Pixels, upper.Pixels, upper.Mask, upper.Opacity, upper.BlendMode);
        }

        parent.Remove(upper);
        document.ActiveLayer = lower;
        document.CommitLayerChange();
        return lower;
    }

    /// <summary>Setzt alle sichtbaren Ebenen zu einer einzigen Ebene zusammen (Bild reduzieren).</summary>
    public static PixelLayer Flatten(ImageDocument document, string layerName)
    {
        var composite = LayerCompositor.Composite(document);
        foreach (var child in document.Root.Children.ToList())
        {
            document.Root.Remove(child);
        }

        var layer = new PixelLayer(layerName, composite);
        document.Root.Add(layer);
        document.ActiveLayer = layer;
        document.CommitLayerChange();
        return layer;
    }

    /// <summary>Vorschlag für einen eindeutigen Namen, z. B. „Ebene 3".</summary>
    public static string NextName(ImageDocument document, string pattern)
    {
        var names = document.Root.Descendants().Select(n => n.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        for (var i = document.LayerCount + 1; ; i++)
        {
            var candidate = string.Format(System.Globalization.CultureInfo.CurrentCulture, pattern, i);
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static void InsertAboveActive(ImageDocument document, LayerNode node)
    {
        if (document.ActiveLayer is { Parent: { } parent } active)
        {
            parent.Insert(parent.IndexOf(active) + 1, node);
        }
        else
        {
            document.Root.Add(node);
        }

        document.ActiveLayer = node;
        document.CommitLayerChange();
    }

    /// <summary>Vormultipliert: alle vier Kanäle mit Maske/255 skalieren (kaufmännisch gerundet).</summary>
    private static void BakeMask(PixelBuffer pixels, LayerMask mask)
    {
        for (var y = 0; y < pixels.Height; y++)
        {
            var row = pixels.GetRow(y);
            var maskRow = mask.GetRow(y);
            for (var x = 0; x < pixels.Width; x++)
            {
                var m = maskRow[x];
                if (m == 255)
                {
                    continue;
                }

                var i = x * 4;
                row[i] = ColorMath.Premultiply(row[i], m);
                row[i + 1] = ColorMath.Premultiply(row[i + 1], m);
                row[i + 2] = ColorMath.Premultiply(row[i + 2], m);
                row[i + 3] = ColorMath.Premultiply(row[i + 3], m);
            }
        }
    }

    private static IEnumerable<PixelLayer> PixelLayersOf(LayerNode node) => node switch
    {
        PixelLayer p => [p],
        LayerGroup g => g.PixelLayers(),
        _ => [],
    };

    private static PixelLayer RequirePixelLayer(ImageDocument document, LayerNode node)
    {
        EnsureInDocument(document, node);
        return node as PixelLayer ?? throw new LayerOperationException(LayerOperationError.NotPixelLayer);
    }

    private static LayerMask RequireMask(ImageDocument document, LayerNode node) =>
        RequirePixelLayer(document, node).Mask ?? throw new LayerOperationException(LayerOperationError.NoMask);

    private static void EnsureInDocument(ImageDocument document, LayerNode node)
    {
        if (node == document.Root || !document.Contains(node))
        {
            throw new LayerOperationException(LayerOperationError.NotInDocument);
        }
    }

    private static void EnsureUnlocked(LayerNode node)
    {
        if (node.IsEffectivelyLocked)
        {
            throw new LayerOperationException(LayerOperationError.Locked);
        }
    }
}
