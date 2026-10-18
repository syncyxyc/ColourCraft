namespace OrbitStudio.Tools;

/// <summary>Einstellbare Werkzeugoption. Beschriftungen sind Textschlüssel („Opt.*"), die Oberfläche übersetzt sie.</summary>
public abstract class ToolOption(string labelKey)
{
    public string LabelKey { get; } = labelKey;

    public event EventHandler? Changed;

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Zahl mit Grenzen. Ungültige Eingaben werden auf den gültigen Bereich begrenzt.</summary>
public sealed class NumberOption : ToolOption
{
    private double _value;

    public NumberOption(string labelKey, double minimum, double maximum, double value, double step = 1,
        string unitKey = "Unit.Px")
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
        DefaultValue = Math.Clamp(value, minimum, maximum);
        _value = DefaultValue;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public string UnitKey { get; }

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
