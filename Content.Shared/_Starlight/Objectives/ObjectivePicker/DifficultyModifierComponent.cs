namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Multiplies the picker budget while this objective belongs to the chooser's mind.
/// Multiple objectives' modifiers multiply together; their own difficulty stays unchanged.
/// </summary>
[RegisterComponent]
public sealed partial class DifficultyModifierComponent : Component
{
    [DataField]
    public float Multiplier = 1;
}
