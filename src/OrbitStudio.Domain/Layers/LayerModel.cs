using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Domain.Layers;

/// <summary>
/// Mischmodi. Formeln (W3C Compositing, separierbar) siehe docs/architecture.md.
/// Die Werte werden in Projektdateien gespeichert – nicht umnummerieren.
/// </summary>
public enum BlendMode
{
    Normal = 0,
    Multiply = 1,
    Screen = 2,
    Add = 3,
    Negation = 4,
    Difference = 5,
    Darken = 6,
    Lighten = 7,
    Overlay = 8,
}

/// <summary>Gemeinsame Eigenschaften von Pixelebenen und Gruppen.</summary>
public abstract class LayerNode
{
    private string _name;
    private double _opacity = 1.0;

    protected LayerNode(string name)
    {
        _name = ValidateName(name);
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => _name = ValidateName(value);
    }

    public bool IsVisible { get; set; } = true;

    /// <summary>Deckkraft 0,0 – 1,0.</summary>
    public double Opacity
    {
        get => _opacity;
        set
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _opacity = Math.Clamp(value, 0.0, 1.0);
        }
    }

    public BlendMode BlendMode { get; set; } = BlendMode.Normal;

    /// <summary>Gesperrte Ebenen dürfen nicht bemalt, gelöscht, verschoben oder zusammengeführt werden.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Übergeordnete Gruppe; null nur für die Wurzel des Dokuments.</summary>
    public LayerGroup? Parent { get; internal set; }

    /// <summary>Sichtbar, wenn die Ebene und alle übergeordneten Gruppen sichtbar sind.</summary>
    public bool IsEffectivelyVisible => IsVisible && (Parent?.IsEffectivelyVisible ?? true);

    /// <summary>Gesperrt, wenn die Ebene oder eine übergeordnete Gruppe gesperrt ist.</summary>
    public bool IsEffectivelyLocked => IsLocked || (Parent?.IsEffectivelyLocked ?? false);

    /// <summary>Verschachtelungstiefe (direkte Kinder der Wurzel = 0).</summary>
    public int Depth => Parent?.Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>Tiefe Kopie mit neuer ID (für „Duplizieren").</summary>
    public abstract LayerNode Duplicate(string name);

    protected void CopyPropertiesTo(LayerNode target)
    {
        target.IsVisible = IsVisible;
        target.Opacity = Opacity;
        target.BlendMode = BlendMode;
        target.IsLocked = false;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Ebenenname darf nicht leer sein.", nameof(name));
        }

        return name.Trim();
    }
}

/// <summary>Ebene mit eigenem Pixelinhalt in Dokumentgröße und optionaler Maske.</summary>
public sealed class PixelLayer : LayerNode
{
    public PixelLayer(string name, PixelBuffer pixels)
        : base(name)
    {
        Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
    }

    public PixelBuffer Pixels { get; }

    public LayerMask? Mask { get; set; }

    public override LayerNode Duplicate(string name)
    {
        var copy = new PixelLayer(name, Pixels.Clone()) { Mask = Mask?.Clone() };
        CopyPropertiesTo(copy);
        return copy;
    }
}

/// <summary>
/// Ebenenmaske: 8 Bit je Pixel, 255 = Ebene sichtbar, 0 = verborgen. Gleiche Größe wie das Dokument.
/// </summary>
public sealed class LayerMask
{
    private readonly byte[] _data;

    private LayerMask(int width, int height, byte[] data)
    {
        Width = width;
        Height = height;
        _data = data;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Deaktivierte Masken bleiben erhalten, wirken aber nicht.</summary>
    public bool IsEnabled { get; set; } = true;

    public Span<byte> Data => _data;

    /// <summary>Neue Maske, vollständig weiß (alles sichtbar).</summary>
    public static LayerMask CreateRevealAll(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var data = new byte[(long)width * height];
        Array.Fill(data, (byte)255);
        return new LayerMask(width, height, data);
    }

    public byte Get(int x, int y) => _data[y * Width + x];

    public void Set(int x, int y, byte value) => _data[y * Width + x] = value;

    public ReadOnlySpan<byte> GetRow(int y) => _data.AsSpan(y * Width, Width);

    public void Fill(IntRect region, byte value)
    {
        var r = region.Intersect(new IntRect(0, 0, Width, Height));
        for (var y = r.Y; y < r.Bottom; y++)
        {
            _data.AsSpan(y * Width + r.X, r.Width).Fill(value);
        }
    }

    public void Invert()
    {
        for (var i = 0; i < _data.Length; i++)
        {
            _data[i] = (byte)(255 - _data[i]);
        }
    }

    public LayerMask Clone() => new(Width, Height, (byte[])_data.Clone()) { IsEnabled = IsEnabled };
}

/// <summary>
/// Gruppe von Ebenen. Kinder sind von unten nach oben geordnet (Index 0 = unterste Ebene).
/// Gruppen werden isoliert zusammengesetzt und dann mit eigener Deckkraft und eigenem Mischmodus eingefügt.
/// </summary>
public sealed class LayerGroup : LayerNode
{
    private readonly List<LayerNode> _children = [];

    public LayerGroup(string name)
        : base(name)
    {
    }

    public IReadOnlyList<LayerNode> Children => _children;

    public int IndexOf(LayerNode node) => _children.IndexOf(node);

    /// <summary>Fügt einen Knoten ein. Er darf noch keiner Gruppe angehören und kein Vorfahre dieser Gruppe sein.</summary>
    public void Insert(int index, LayerNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Parent is not null)
        {
            throw new InvalidOperationException("Die Ebene gehört bereits zu einer Gruppe.");
        }

        if (node == this || (node is LayerGroup group && IsDescendantOf(group)))
        {
            throw new InvalidOperationException("Eine Gruppe kann nicht in sich selbst eingefügt werden.");
        }

        if (index < 0 || index > _children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _children.Insert(index, node);
        node.Parent = this;
    }

    public void Add(LayerNode node) => Insert(_children.Count, node);

    public void Remove(LayerNode node)
    {
        if (_children.Remove(node))
        {
            node.Parent = null;
        }
    }

    /// <summary>Verschiebt ein Kind innerhalb dieser Gruppe an einen neuen Index.</summary>
    public void Move(LayerNode node, int newIndex)
    {
        var oldIndex = _children.IndexOf(node);
        if (oldIndex < 0)
        {
            throw new InvalidOperationException("Die Ebene gehört nicht zu dieser Gruppe.");
        }

        if (newIndex < 0 || newIndex >= _children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        }

        _children.RemoveAt(oldIndex);
        _children.Insert(newIndex, node);
    }

    /// <summary>Alle Knoten unterhalb dieser Gruppe in Zeichenreihenfolge (unten zuerst, Gruppe vor ihren Kindern).</summary>
    public IEnumerable<LayerNode> Descendants()
    {
        foreach (var child in _children)
        {
            yield return child;
            if (child is LayerGroup group)
            {
                foreach (var nested in group.Descendants())
                {
                    yield return nested;
                }
            }
        }
    }

    public IEnumerable<PixelLayer> PixelLayers() => Descendants().OfType<PixelLayer>();

    public override LayerNode Duplicate(string name)
    {
        var copy = new LayerGroup(name);
        CopyPropertiesTo(copy);
        foreach (var child in _children)
        {
            copy.Add(child.Duplicate(child.Name));
        }

        return copy;
    }

    private bool IsDescendantOf(LayerGroup group)
    {
        for (var p = Parent; p is not null; p = p.Parent)
        {
            if (p == group)
            {
                return true;
            }
        }

        return false;
    }
}
