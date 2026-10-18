using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Tools;

/// <summary>Einstellbare Werkzeugoption. Beschriftungen sind Textschlüssel („Opt.*"), die Oberfläche übersetzt sie.</summary>
public abstract class ToolOption(string labelKey)
{
    private bool _isEnabled = true;

    public string LabelKey { get; } = labelKey;

    /// <summary>False: Option ist sichtbar, aber nicht bedienbar (z. B. Koordinaten ohne laufende Bearbeitung).</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Der Wert hat sich geändert (durch die Oberfläche oder durch das Werkzeug).</summary>
    public event EventHandler? Changed;

    public event EventHandler? EnabledChanged;

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Zahl mit Grenzen. Ungültige Eingaben werden auf den gültigen Bereich begrenzt.</summary>
public sealed class NumberOption : ToolOption
{
    private double _value;

    public NumberOption(string labelKey, double minimum, double maximum, double value, double step = 1,
        string unitKey = "Unit.Px", bool showSlider = true)
        : base(labelKey)
    {
        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum größer als Maximum.");
        }

        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        UnitKey = unitKey;
        ShowSlider = showSlider;
        DefaultValue = Math.Clamp(value, minimum, maximum);
        _value = DefaultValue;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public string UnitKey { get; }

    /// <summary>False: nur Zahlenfeld (z. B. Koordinaten mit großem Wertebereich).</summary>
    public bool ShowSlider { get; }

    public double DefaultValue { get; }

    public double Value
    {
        get => _value;
        set
        {
            var clamped = double.IsFinite(value) ? Math.Clamp(value, Minimum, Maximum) : _value;
            if (clamped != _value)
            {
                _value = clamped;
                RaiseChanged();
            }
        }
    }

    public void Reset() => Value = DefaultValue;
}

public sealed class ToggleOption(string labelKey, bool value) : ToolOption(labelKey)
{
    private bool _value = value;

    public bool DefaultValue { get; } = value;

    public void Reset() => Value = DefaultValue;

    public bool Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                RaiseChanged();
            }
        }
    }
}

/// <summary>Auswahl aus festen Möglichkeiten (Textschlüssel je Eintrag).</summary>
public sealed class ChoiceOption : ToolOption
{
    private int _index;

    public ChoiceOption(string labelKey, IReadOnlyList<string> choiceKeys, int index = 0)
        : base(labelKey)
    {
        if (choiceKeys.Count == 0)
        {
            throw new ArgumentException("Mindestens eine Auswahl nötig.", nameof(choiceKeys));
        }

        ChoiceKeys = choiceKeys;
        _index = Math.Clamp(index, 0, choiceKeys.Count - 1);
    }

    public IReadOnlyList<string> ChoiceKeys { get; }

    public int Index
    {
        get => _index;
        set
        {
            var clamped = Math.Clamp(value, 0, ChoiceKeys.Count - 1);
            if (clamped != _index)
            {
                _index = clamped;
                RaiseChanged();
            }
        }
    }
}

/// <summary>Farbe inklusive Alpha (gerades Alpha).</summary>
public sealed class ColorOption(string labelKey, ColorRgba value) : ToolOption(labelKey)
{
    private ColorRgba _value = value;

    public ColorRgba DefaultValue { get; } = value;

    public ColorRgba Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                RaiseChanged();
            }
        }
    }

    public void Reset() => Value = DefaultValue;
}

/// <summary>
/// Schaltfläche in der Optionsleiste (z. B. „Anwenden"). Die Oberfläche übergibt den Kontext des Dokuments, auf dem
/// das Werkzeug zuletzt gearbeitet hat (null, wenn es keines gibt).
/// </summary>
public sealed class ActionOption(string labelKey, Action<ToolContext?> execute) : ToolOption(labelKey)
{
    public void Execute(ToolContext? context)
    {
        if (IsEnabled)
        {
            execute(context);
        }
    }
}

/// <summary>Nur-Lese-Anzeige berechneter Werte. Text = Format(Textschlüssel, Argumente).</summary>
public sealed class InfoOption(string labelKey, string textKey) : ToolOption(labelKey)
{
    public string TextKey { get; private set; } = textKey;

    public IReadOnlyList<object> Arguments { get; private set; } = [];

    public void Set(string textKey, params object[] arguments)
    {
        TextKey = textKey;
        Arguments = arguments;
        RaiseChanged();
    }
}
