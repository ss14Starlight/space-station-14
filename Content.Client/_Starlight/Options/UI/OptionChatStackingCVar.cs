using Content.Client.Options.UI;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Options.UI;

/// <summary>
///     Chat stacking options.
/// </summary>
public sealed class OptionChatStackingCVar : BaseOption
{
    private readonly IConfigurationManager _cfg;
    private readonly CVarDef<int> _cVar;
    private readonly CheckBox _checkBox;
    private readonly OptionSlider _slider;
    private readonly int _minValue;
    private readonly int _maxValue;
    private int _value;
    private int _lastEnabledValue;
    private bool _updating;

    /// <summary>
    ///     Creates a new chat stacking option.
    /// </summary>
    /// <param name="controller">The control row that owns this option.</param>
    /// <param name="cfg">The configuration manager to get and set values from.</param>
    /// <param name="cVar">The CVar that controls how many messages may be stacked onto.</param>
    /// <param name="checkBox">The checkbox that toggles stacking on and off.</param>
    /// <param name="slider">The slider that controls how many recent messages may be stacked onto.</param>
    /// <param name="minValue">The minimum value the slider should allow.</param>
    /// <param name="maxValue">The maximum value the slider should allow.</param>
    public OptionChatStackingCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<int> cVar,
        CheckBox checkBox,
        OptionSlider slider,
        int minValue,
        int maxValue) : base(controller)
    {
        _cfg = cfg;
        _cVar = cVar;
        _checkBox = checkBox;
        _slider = slider;
        _minValue = minValue;
        _maxValue = maxValue;
        _lastEnabledValue = Math.Max(1, cVar.DefaultValue);

        slider.Slider.MinValue = minValue;
        slider.Slider.MaxValue = maxValue;
        slider.Slider.Rounded = true;

        checkBox.OnToggled += _ => OnToggled();
        slider.Slider.OnValueChanged += _ => OnSliderChanged();
    }

    /// <inheritdoc/>
    public override void LoadValue()
        => SetValue(_cfg.GetCVar(_cVar));

    /// <inheritdoc/>
    public override void SaveValue()
        => _cfg.SetCVar(_cVar, _value);

    /// <inheritdoc/>
    public override void ResetToDefault()
        => SetValue(_cVar.DefaultValue);

    /// <inheritdoc/>
    public override bool IsModified()
        => _value != _cfg.GetCVar(_cVar);

    /// <inheritdoc/>
    public override bool IsModifiedFromDefault()
        => _value != _cVar.DefaultValue;

    private void OnToggled()
    {
        if (_updating)
            return;

        SetValue(_checkBox.Pressed ? _lastEnabledValue : 0);
        ValueChanged();
    }

    private void OnSliderChanged()
    {
        if (_updating)
            return;

        SetValue((int) _slider.Slider.Value);
        ValueChanged();
    }

    private void SetValue(int value)
    {
        _value = Math.Clamp(value, _minValue, _maxValue);

        if (_value > 0)
            _lastEnabledValue = _value;

        _updating = true;
        _slider.Slider.Value = _value;
        _checkBox.Pressed = _value > 0;
        _updating = false;

        _slider.ValueLabel.Text = _value == 0
            ? Loc.GetString("ui-options-chat-stacking-disabled")
            : _value.ToString();
    }
}
