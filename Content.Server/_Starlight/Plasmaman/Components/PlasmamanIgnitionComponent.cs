using Content.Shared.Atmos;

namespace Content.Server._Starlight.Plasmaman.Components;

/// <summary>
/// Makes plasmamen ignite when oxygen is present and their protective suit pieces are missing.
/// </summary>
[RegisterComponent]
public sealed partial class PlasmamanIgnitionComponent : Component
{
    /// <summary>
    /// Gas that triggers ignition on contact with unprotected skin.
    /// </summary>
    [DataField]
    public Gas Gas = Gas.Oxygen;

    /// <summary>
    /// Minimum moles of the trigger gas in the tile to start ignition.
    /// </summary>
    [DataField]
    public float MolesToIgnite = 0.5f;

    /// <summary>
    /// Fire stacks added per tick when no protective gear is worn at all.
    /// </summary>
    [DataField]
    public float BaseFireStacks = 0.13f;

    /// <summary>
    /// Additional fire stacks added when the jumpsuit slot is unprotected.
    /// </summary>
    [DataField]
    public float JumpsuitFireStacks = 0.12f;

    /// <summary>
    /// Additional fire stacks added when the head slot is unprotected.
    /// </summary>
    [DataField]
    public float HeadFireStacks = 0.02f;

    /// <summary>
    /// Additional fire stacks added when the gloves slot is unprotected.
    /// </summary>
    [DataField]
    public float GlovesFireStacks = 0.04f;
}
