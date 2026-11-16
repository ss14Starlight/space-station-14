namespace Content.Shared._Starlight.Movement.Components;

/// <summary>
/// Colors the surface footstep of whoever wears these shoes, so boots, dress shoes and slippers differ.
/// Ignored when the shoes play their own sound through FootstepModifier.
/// </summary>
[RegisterComponent]
public sealed partial class FootstepTimbreComponent : Component
{
    [DataField]
    public float Pitch = 1f;

    [DataField]
    public float Volume;
}
