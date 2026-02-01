using Content.Shared.EntityEffects;

namespace Content.Shared._Starlight.EntityEffects.Effects.Atmos;

/// <summary>
/// See serverside system.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class AddHeat : EntityEffectBase<AddHeat>
{
    /// <summary>
    ///     The amount of heat we're adding.
    /// </summary>
    [DataField]
    public float Heat;
}