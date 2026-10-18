using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Effects;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.Views;
using static OrbitStudio.Presentation.Commands.CommandIds;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>
/// Menüs „Farben" und „Effekte": Effektdialog mit Vorschau, dann Berechnung als abbrechbarer Hintergrundjob (Esc) auf
/// einer Kopie der aktiven Ebene; die Übernahme ist ein Rückgängig-Schritt. Während der Berechnung sind Befehle und
/// Werkzeugeingaben gesperrt, damit die Ebene nicht parallel verändert wird.
/// </summary>
public sealed partial class MainViewModel
{
    private readonly Dictionary<string, EffectSettings> _effectSettings = new(StringComparer.Ordinal);
    private CancellationTokenSource? _effectCancellation;

    /// <summary>Menübefehl → Effekt. Einzeleffekte Helligkeit, Kontrast, Farbton, Sättigung sind über die kombinierten Dialoge erreichbar.</summary>
    public static IReadOnlyList<(string CommandId, string TextKey, string EffectId, string? Gesture)> EffectCommands { get; } =
    [
        (AdjustBrightnessContrast, "Cmd.Adjust.BrightnessContrast", "brightnessContrast", null),
        (AdjustHueSaturation, "Cmd.Adjust.HueSaturation", "hueSaturation", "Ctrl+U"),
        (AdjustLevels, "Cmd.Adjust.Levels", "levels", "Ctrl+L"),
        (AdjustChannels, "Cmd.Adjust.Channels", "channels", null),
        (AdjustBlackWhite, "Cmd.Adjust.BlackWhite", "blackWhite", "Ctrl+Shift+B"),
        (AdjustInvert, "Cmd.Adjust.Invert", "invert", "Ctrl+Shift+I"),
        (AdjustSepia, "Cmd.Adjust.Sepia", "sepia", null),
        (EffectBlur, "Cmd.Effect.Blur", "gaussianBlur", null),
        (EffectBoxBlur, "Cmd.Effect.BoxBlur", "boxBlur", null),
        (EffectSharpen, "Cmd.Effect.Sharpen", "sharpen", null),
        (EffectNoise, "Cmd.Effect.Noise", "noise", null),
        (EffectDenoise, "Cmd.Effect.Denoise", "denoise", null),
        (EffectEdges, "Cmd.Effect.Edges", "edges", null),
        (EffectShadow, "Cmd.Effect.Shadow", "dropShadow", null),
        (EffectDistort, "Cmd.Effect.Distort", "pixelate", null),
    ];

    /// <summary>Alle Effekte (Standardsatz; Plugins ergänzen ab M10).</summary>
    public EffectRegistry Effects { get; } = EffectRegistry.CreateDefault();

    /// <summary>True, solange ein Effekt berechnet wird (Esc bricht ab).</summary>
    public bool IsEffectRunning => _effectCancellation is not null;

    /// <summary>Letzte bestätigte Einstellungen je Effekt (Startwerte beim nächsten Öffnen des Dialogs).</summary>
    public EffectSettings? LastSettings(string effectId) => _effectSettings.GetValueOrDefault(effectId);

    private void RegisterEffectCommands()
    {
        foreach (var (commandId, textKey, effectId, gesture) in EffectCommands)
        {
            var effect = Effects.Get(effectId);
            _registry.Register(AppCommand.Create(commandId, textKey, () => RunAsync(() => ApplyEffectAsync(effect)),
                CanApplyEffect, gesture));
        }
    }

    private bool CanApplyEffect() => !IsBusy && Doc?.ActivePixelLayer is { IsEffectivelyLocked: false };

    /// <summary>Dialog (falls der Effekt Parameter hat), dann Berechnung im Hintergrund und Übernahme.</summary>
    public async Task ApplyEffectAsync(IEffect effect)
    {
        if (Doc is not { ActivePixelLayer: { } layer } document || IsBusy)
        {
            return;
        }

        Tools.CommitActive();
        if (layer.IsEffectivelyLocked)
        {
            StatusMessage = Loc.Get("ToolProblem.LayerLocked");
            return;
        }

        var settings = LastSettings(effect.Id) ?? EffectSettings.Defaults(effect);
        if (effect.Parameters.Count > 0)
        {
            var preview = EffectEngine.CreatePreview(layer.Pixels, document.Selection, EffectDialog.PreviewWidth,
                EffectDialog.PreviewHeight);
            if (_dialogs.ShowEffect(new EffectDialogRequest(effect, settings, preview)) is not { } chosen)
            {
                return;
            }

            settings = chosen;
            _effectSettings[effect.Id] = chosen;
        }

        await RunEffectAsync(document, layer, effect, settings);
    }

    /// <summary>Bricht eine laufende Effektberechnung ab. Liefert false, wenn keine läuft.</summary>
    public bool CancelEffect()
    {
        if (_effectCancellation is not { } cancellation)
        {
            return false;
        }

        cancellation.Cancel();
        return true;
    }

    private async Task RunEffectAsync(ImageDocument document, PixelLayer layer, IEffect effect, EffectSettings settings)
    {
        var name = Loc.WithoutAccessKey(Loc.Get(effect.NameKey));
        var backup = layer.Pixels.Clone();
        var selection = document.Selection?.Clone();
        using var cancellation = new CancellationTokenSource();
        _effectCancellation = cancellation;
        IsBusy = true;
        Tools.InputBlocked = true;
        StatusMessage = Loc.Format("Status.EffectRunning", name);
        try
        {
            var token = cancellation.Token;
            var result = await Task.Run(() => EffectEngine.Render(effect, settings, backup, selection, token), token);
            if (!_workspace.Documents.Contains(document) || !document.Contains(layer))
            {
                StatusMessage = Loc.Format("Status.EffectCancelled", name);
                return;
            }

            EffectEngine.Commit(document, _histories.For(document), layer, backup, result, effect.NameKey);
            StatusMessage = Loc.Format("Status.EffectApplied", name);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Format("Status.EffectCancelled", name);
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Nicht genug Speicher für Effekt {Effect}.", effect.Id);
            _dialogs.ShowError(Loc.Get("Msg.Error.Title"), Loc.Format("Msg.File.OutOfMemory", document.Name));
        }
        catch (EffectValidationException ex)
        {
            _logger.LogWarning(ex, "Ungültige Effektparameter.");
            _dialogs.ShowWarning(Loc.Get("Msg.Effect.Title"), string.Join(Environment.NewLine,
                ex.Errors.Select(e => Loc.Get(e.MessageKey)).Distinct()));
        }
        finally
        {
            _effectCancellation = null;
            Tools.InputBlocked = false;
            var status = StatusMessage;
            IsBusy = false;
            StatusMessage = status;
            _registry.NotifyStateChanged();
        }
    }
}
