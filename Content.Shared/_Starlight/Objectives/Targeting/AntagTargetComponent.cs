namespace Content.Shared._Starlight.Objectives.Targeting;

/// <summary>
/// Allows a non-humanoid character to be chosen by antagonist objective target pools.
/// The character must still have a living body and satisfy the objective's target filters.
/// </summary>
[RegisterComponent]
public sealed partial class AntagTargetComponent : Component;
