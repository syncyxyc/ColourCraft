namespace OrbitStudio.Domain.Imaging;

/// <summary>Position mit Fließkommagenauigkeit in Dokumentkoordinaten (unabhängig von Bildschirmauflösung).</summary>
public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);

    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);

    public static PointD operator *(PointD a, double f) => new(a.X * f, a.Y * f);

    public double Length => Math.Sqrt(X * X + Y * Y);
}

/// <summary>Ganzzahliges Rechteck in Pixeln. Rechte und untere Kante sind exklusiv.</summary>
public readonly record struct IntRect(int X, int Y, int Width, int Height)
{
    public static readonly IntRect Empty = new(0, 0, 0, 0);

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;

    public IntRect Intersect(IntRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? new IntRect(left, top, right - left, bottom - top) : Empty;
    }

    public IntRect Union(IntRect other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        return new IntRect(left, top, Math.Max(Right, other.Right) - left, Math.Max(Bottom, other.Bottom) - top);
    }
}
