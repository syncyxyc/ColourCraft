using System.Globalization;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.Services;

/// <summary>Prüft die Eingaben des Dialogs „Neues Bild" und liefert eine deutsche Fehlermeldung.</summary>
public static class NewDocumentValidator
{
    public static (NewDocumentRequest? Request, string? Error) Validate(string? name, string? widthText,
        string? heightText, DocumentBackground background)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, Loc.Get("Dlg.New.Error.Name"));
        }

        if (!int.TryParse(widthText?.Trim(), NumberStyles.Integer, Loc.Culture, out var width)
            || !int.TryParse(heightText?.Trim(), NumberStyles.Integer, Loc.Culture, out var height))
        {
            return (null, Loc.Get("Dlg.New.Error.Number"));
        }

        var error = SizeMessage(DocumentLimits.ValidateSize(width, height));
        return error is null
            ? (new NewDocumentRequest(name.Trim(), width, height, background), null)
            : (null, error);
    }

    public static string? SizeMessage(SizeValidationResult result) => result switch
    {
        SizeValidationResult.Valid => null,
        SizeValidationResult.NonPositive => Loc.Get("Msg.Size.NonPositive"),
        SizeValidationResult.ExceedsMaxDimension => Loc.Format("Msg.Size.ExceedsMaxDimension", DocumentLimits.MaxDimension),
        SizeValidationResult.ExceedsMaxPixelCount => Loc.Format("Msg.Size.ExceedsMaxPixelCount", DocumentLimits.MaxPixelCount),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
    };
}
