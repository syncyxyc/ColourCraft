namespace OrbitStudio.Domain.Documents;

/// <summary>
/// Harte Grenzen für Dokumentgrößen. Schützt vor Speicherüberlauf durch übergroße Bilder.
/// </summary>
public static class DocumentLimits
{
    /// <summary>Maximale Kantenlänge in Pixeln.</summary>
    public const int MaxDimension = 65_535;

    /// <summary>
    /// Maximale Pixelanzahl je Dokument (ca. 268 MP). Bei BGRA 8 Bit entspricht das 1 GiB je Ebene.
    /// </summary>
    public const long MaxPixelCount = 16_384L * 16_384L;

    public const double MinResolutionDpi = 1.0;
    public const double MaxResolutionDpi = 10_000.0;

    public static SizeValidationResult ValidateSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return SizeValidationResult.NonPositive;
        }

        if (width > MaxDimension || height > MaxDimension)
        {
            return SizeValidationResult.ExceedsMaxDimension;
        }

        if ((long)width * height > MaxPixelCount)
        {
            return SizeValidationResult.ExceedsMaxPixelCount;
        }

        return SizeValidationResult.Valid;
    }

    public static bool IsValidResolution(double dpi) =>
        double.IsFinite(dpi) && dpi >= MinResolutionDpi && dpi <= MaxResolutionDpi;
}

/// <summary>Ergebnis der Größenprüfung. Texte für die Oberfläche liegen in der Presentation-Schicht.</summary>
public enum SizeValidationResult
{
    Valid,
    NonPositive,
    ExceedsMaxDimension,
    ExceedsMaxPixelCount,
}
