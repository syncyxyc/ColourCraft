using System.Collections.Immutable;
using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects;

/// <summary>Kontrollpunkt einer Tonwertkurve: Eingangswert X → Ausgangswert Y (beide 0–255).</summary>
public readonly record struct CurvePoint(int X, int Y);

/// <summary>
/// Tonwertkurve (Farbkurven): unveränderliche Liste von Kontrollpunkten mit streng steigendem X. Zwischen den Punkten
/// wird monoton kubisch interpoliert (Fritsch–Carlson): Die Kurve läuft genau durch alle Punkte und überschwingt
/// zwischen zwei Punkten nie über deren Y-Werte hinaus. Links vom ersten und rechts vom letzten Punkt bleibt sie waagerecht.
/// Die Änderungsmethoden sind die Operationen des Kurveneditors; sie liefern immer eine gültige Kurve.
/// </summary>
public sealed class ToneCurve : IEquatable<ToneCurve>
{
    public const int MinimumPoints = 2;
    public const int MaximumPoints = 16;
    public const int ValueMax = 255;

    /// <summary>Kleinster Abstand zweier Kontrollpunkte in X.</summary>
    public const int MinimumGap = 1;

    private readonly ImmutableArray<CurvePoint> _points;
    private byte[]? _lut;

    private ToneCurve(ImmutableArray<CurvePoint> points) => _points = points;

    /// <summary>Gerade von (0, 0) nach (255, 255): ändert nichts.</summary>
    public static ToneCurve Identity { get; } = new([new CurvePoint(0, 0), new CurvePoint(ValueMax, ValueMax)]);

    public IReadOnlyList<CurvePoint> Points => _points;

    public bool IsIdentity => ToLookupTable().AsSpan().SequenceEqual(Identity.ToLookupTable());

    /// <summary>Prüft Punkte; liefert null oder eine deutsche Fehlermeldung.</summary>
    public static string? Validate(IReadOnlyList<CurvePoint>? points)
    {
        if (points is null || points.Count < MinimumPoints)
        {
            return $"Eine Kurve braucht mindestens {MinimumPoints} Punkte.";
        }

        if (points.Count > MaximumPoints)
        {
            return $"Eine Kurve hat höchstens {MaximumPoints} Punkte.";
        }

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.X is < 0 or > ValueMax || p.Y is < 0 or > ValueMax)
            {
                return "Punkte müssen im Bereich 0 bis 255 liegen.";
            }

            if (i > 0 && p.X - points[i - 1].X < MinimumGap)
            {
                return "Die X-Werte der Punkte müssen streng aufsteigen.";
            }
        }

        return null;
    }

    public static ToneCurve Create(IEnumerable<CurvePoint> points)
    {
        var list = points.ToImmutableArray();
        var error = Validate(list);
        return error is null ? new ToneCurve(list) : throw new ArgumentException(error, nameof(points));
    }

    /// <summary>Fügt einen Punkt ein. Liegt X bereits auf einem Punkt (oder näher als der Mindestabstand), wird dieser verschoben.</summary>
    public ToneCurve AddPoint(int x, int y)
    {
        x = Math.Clamp(x, 0, ValueMax);
        y = Math.Clamp(y, 0, ValueMax);
        var existing = _points.ToList().FindIndex(p => Math.Abs(p.X - x) < MinimumGap);
        if (existing >= 0)
        {
            return MovePoint(existing, x, y);
        }

        if (_points.Length >= MaximumPoints)
        {
            return this;
        }

        var list = _points.Add(new CurvePoint(x, y)).Sort((a, b) => a.X.CompareTo(b.X));
        return new ToneCurve(list);
    }

    /// <summary>
    /// Verschiebt einen Punkt. X wird auf die Lücke zwischen den Nachbarn begrenzt (Reihenfolge bleibt erhalten), Y auf 0–255.
    /// </summary>
    public ToneCurve MovePoint(int index, int x, int y)
    {
        if ((uint)index >= (uint)_points.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var minX = index == 0 ? 0 : _points[index - 1].X + MinimumGap;
        var maxX = index == _points.Length - 1 ? ValueMax : _points[index + 1].X - MinimumGap;
        var moved = new CurvePoint(Math.Clamp(x, minX, maxX), Math.Clamp(y, 0, ValueMax));
        return new ToneCurve(_points.SetItem(index, moved));
    }

    /// <summary>Entfernt einen Punkt; die letzten beiden Punkte bleiben immer erhalten.</summary>
    public ToneCurve RemovePoint(int index)
    {
        if ((uint)index >= (uint)_points.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _points.Length <= MinimumPoints ? this : new ToneCurve(_points.RemoveAt(index));
    }

    /// <summary>Index des Punktes, der dem Zeiger (in Kurvenkoordinaten) am nächsten liegt und höchstens <paramref name="tolerance"/> entfernt ist; sonst −1.</summary>
    public int HitTest(double x, double y, double tolerance)
    {
        var best = -1;
        var bestDistance = tolerance * tolerance;
        for (var i = 0; i < _points.Length; i++)
        {
            var dx = _points[i].X - x;
            var dy = _points[i].Y - y;
            var distance = dx * dx + dy * dy;
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Ausgangswert für Eingang <paramref name="x"/> (0–255, auch Zwischenwerte); nicht gerundet, begrenzt auf 0–255.</summary>
    public double Evaluate(double x)
    {
        var n = _points.Length;
        if (x <= _points[0].X)
        {
            return _points[0].Y;
        }

        if (x >= _points[n - 1].X)
        {
            return _points[n - 1].Y;
        }

        var k = 0;
        while (x > _points[k + 1].X)
        {
            k++;
        }

        var tangents = Tangents();
        var x0 = _points[k].X;
        var h = _points[k + 1].X - x0;
        var t = (x - x0) / h;
        var t2 = t * t;
        var t3 = t2 * t;
        var y = (2 * t3 - 3 * t2 + 1) * _points[k].Y
                + (t3 - 2 * t2 + t) * h * tangents[k]
                + (-2 * t3 + 3 * t2) * _points[k + 1].Y
                + (t3 - t2) * h * tangents[k + 1];
        return Math.Clamp(y, 0, ValueMax);
    }

    /// <summary>Tabelle mit 256 Einträgen: Eingang → gerundeter Ausgang (kaufmännisch, 0–255).</summary>
    public byte[] ToLookupTable()
    {
        if (_lut is null)
        {
            var lut = new byte[ValueMax + 1];
            for (var i = 0; i <= ValueMax; i++)
            {
                lut[i] = PixelMath.ToByte(Evaluate(i));
            }

            _lut = lut;
        }

        return _lut;
    }

    private double[] Tangents()
    {
        var n = _points.Length;
        var secant = new double[n - 1];
        for (var i = 0; i < n - 1; i++)
        {
            secant[i] = (double)(_points[i + 1].Y - _points[i].Y) / (_points[i + 1].X - _points[i].X);
        }

        var m = new double[n];
        m[0] = secant[0];
        m[n - 1] = secant[n - 2];
        for (var i = 1; i < n - 1; i++)
        {
            m[i] = secant[i - 1] * secant[i] <= 0 ? 0 : (secant[i - 1] + secant[i]) / 2;
        }

        // Fritsch–Carlson: Steigungen so begrenzen, dass die Kurve zwischen zwei Punkten monoton bleibt.
        for (var i = 0; i < n - 1; i++)
        {
            if (secant[i] == 0)
            {
                m[i] = 0;
                m[i + 1] = 0;
                continue;
            }

            var a = m[i] / secant[i];
            var b = m[i + 1] / secant[i];
            var s = a * a + b * b;
            if (s > 9)
            {
                var tau = 3 / Math.Sqrt(s);
                m[i] = tau * a * secant[i];
                m[i + 1] = tau * b * secant[i];
            }
        }

        return m;
    }

    public bool Equals(ToneCurve? other) => other is not null && _points.AsSpan().SequenceEqual(other._points.AsSpan());

    public override bool Equals(object? obj) => obj is ToneCurve other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var p in _points)
        {
            hash.Add(p);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Kurvenparameter eines Effekts; Standardwert ist die Gerade (<see cref="ToneCurve.Identity"/>).</summary>
public sealed record CurveParameter(string Id, string LabelKey) : EffectParameter(Id, LabelKey)
{
    public override object DefaultValue => ToneCurve.Identity;

    public override string? Validate(object? value) => value is ToneCurve ? null : "Effect.Error.Type";
}

/// <summary>
/// Farbkurven: je eine Tonwertkurve für Rot, Grün und Blau sowie eine für alle Kanäle („Helligkeit"). Auf die gerade
/// Farbe wird zuerst die Gesamtkurve und danach die Kanalkurve angewendet: c' = Kanal[Gesamt[c]] (c auf ganze Zahlen gerundet).
/// Alpha bleibt unverändert.
/// </summary>
public sealed class CurvesEffect : PointEffect
{
    public static readonly CurveParameter Master = new("master", "EffectParam.CurveMaster");
    public static readonly CurveParameter Red = new("red", "EffectParam.CurveRed");
    public static readonly CurveParameter Green = new("green", "EffectParam.CurveGreen");
    public static readonly CurveParameter Blue = new("blue", "EffectParam.CurveBlue");

    public override string Id => "curves";

    public override IReadOnlyList<EffectParameter> Parameters => [Master, Red, Green, Blue];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var master = settings.Curve(Master.Id).ToLookupTable();
        var red = settings.Curve(Red.Id).ToLookupTable();
        var green = settings.Curve(Green.Id).ToLookupTable();
        var blue = settings.Curve(Blue.Id).ToLookupTable();
        return (r, g, b, _, _) => (
            red[master[PixelMath.ToByte(r)]],
            green[master[PixelMath.ToByte(g)]],
            blue[master[PixelMath.ToByte(b)]]);
    }
}
