using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Tools.Painting;

/// <summary>
/// Abdeckung (0–1) eines Pixels durch eine Form, ausgewertet am Pixelmittelpunkt (x + 0,5; y + 0,5).
/// Kantenglättung: linearer Übergang über 1 Pixel an der Formgrenze (Abstand −0,5 … +0,5).
/// </summary>
public static class Coverage
{
    /// <summary>Kürzester Abstand des Punkts p zur Strecke a–b.</summary>
    public static double DistanceToSegment(PointD p, PointD a, PointD b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lengthSquared = abx * abx + aby * aby;
        var t = lengthSquared <= 0 ? 0 : Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lengthSquared, 0, 1);
        var dx = p.X - (a.X + t * abx);
        var dy = p.Y - (a.Y + t * aby);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Pinselprofil abhängig vom Abstand zur Mittellinie. Härte 1 = harte Kante; kleinere Härte = weicher Rand
    /// (innerer Radius = Radius · Härte, danach glatter Abfall).
    /// </summary>
    public static double BrushProfile(double distance, double radius, double hardness, bool antialias)
    {
        if (!antialias)
        {
            return distance <= radius ? 1 : 0;
        }

        if (hardness >= 0.999)
        {
            return Math.Clamp(radius - distance + 0.5, 0, 1);
        }

        var inner = radius * Math.Clamp(hardness, 0, 1);
        if (distance <= inner)
        {
            return 1;
        }

        var outer = radius + 0.5;
        if (distance >= outer)
        {
            return 0;
        }

        var t = (outer - distance) / (outer - inner);
        return t * t * (3 - 2 * t); // weicher Übergang (Smoothstep)
    }

    /// <summary>Signierter Abstand zu einem Rechteck (negativ innen).</summary>
    public static double SignedDistanceToBox(PointD p, double left, double top, double right, double bottom)
    {
        var cx = (left + right) / 2;
        var cy = (top + bottom) / 2;
        var hx = (right - left) / 2;
        var hy = (bottom - top) / 2;
        var qx = Math.Abs(p.X - cx) - hx;
        var qy = Math.Abs(p.Y - cy) - hy;
        var outside = Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0));
        return outside + Math.Min(Math.Max(qx, qy), 0);
    }

    /// <summary>Näherung des signierten Abstands zu einer Ellipse (Gradientenverfahren, genau an der Kante).</summary>
    public static double SignedDistanceToEllipse(PointD p, double cx, double cy, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0)
        {
            return double.PositiveInfinity;
        }

        var dx = p.X - cx;
        var dy = p.Y - cy;
        var f = dx * dx / (rx * rx) + dy * dy / (ry * ry) - 1;
        var gx = 2 * dx / (rx * rx);
        var gy = 2 * dy / (ry * ry);
        var gradient = Math.Sqrt(gx * gx + gy * gy);
        return gradient < 1e-12 ? -Math.Min(rx, ry) : f / gradient;
    }

    /// <summary>Abdeckung aus einem signierten Abstand (Kantenglättung über 1 Pixel).</summary>
    public static double FromSignedDistance(double signedDistance, bool antialias) =>
        antialias ? Math.Clamp(0.5 - signedDistance, 0, 1) : signedDistance <= 0 ? 1 : 0;
}
