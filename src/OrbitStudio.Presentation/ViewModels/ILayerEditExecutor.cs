using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Layers;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>
/// Führt Ebenenänderungen aus dem Ebenen-Panel mit Historie aus (wird vom Hauptfenster bereitgestellt).
/// </summary>
public interface ILayerEditExecutor
{
    /// <param name="coalesceKey">Gleichartige Folgeschritte (z. B. Reglerbewegung) werden zusammengefasst.</param>
    void Execute(ImageDocument document, string nameKey, Action operation, string? coalesceKey = null,
        IEnumerable<PixelLayer>? changedPixels = null, IEnumerable<LayerMask>? changedMasks = null);
}

/// <summary>Ausführung ohne Historie (Standard, z. B. in Tests ohne Hauptfenster).</summary>
public sealed class DirectLayerEditExecutor : ILayerEditExecutor
{
    public static readonly DirectLayerEditExecutor Instance = new();

    public void Execute(ImageDocument document, string nameKey, Action operation, string? coalesceKey = null,
        IEnumerable<PixelLayer>? changedPixels = null, IEnumerable<LayerMask>? changedMasks = null) => operation();
}
