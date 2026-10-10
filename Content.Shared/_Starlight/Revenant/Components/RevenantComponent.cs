using System.Numerics;
using Content.Shared.FixedPoint;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Revenant.Components;

// Starlight: datafields for the Starlight-only revenant abilities. Kept out of the
// upstream RevenantComponent.cs so that file carries no Starlight diff.
public sealed partial class RevenantComponent
{
    #region Misfire Ability
    /// <summary>
    /// The amount of essence that is needed to use the ability.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("misfireCost")]
    public FixedPoint2 misfireCost = 10;

    /// <summary>
    /// The status effects applied after the ability
    /// the first float corresponds to amount of time the entity is stunned.
    /// the second corresponds to the amount of time the entity is made solid.
    /// </summary>
    [DataField("MisfireDebuffs")]
    public Vector2 MisfireDebuffs = new(1, 25);

    /// <summary>
    /// How close to the gun the entity has to be in order to be targeted.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("misfireTargetRadius")]
    public float MisfireTargetRadius = 14f;

    #endregion

    #region Chill of the grave Ability
    /// <summary>
    /// The amount of essence that is needed to use the ability.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("chillingTouchCost")]
    public FixedPoint2 chillCost = 50;

    /// <summary>
    /// The status effects applied after the ability
    /// the first float corresponds to amount of time the entity is stunned.
    /// the second corresponds to the amount of time the entity is made solid.
    /// </summary>
    [DataField("ChillDebuffs")]
    public Vector2 ChillDebuffs = new(2, 10);

    /// <summary>
    /// The radius around the user that this ability affects guaranteed
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("chillCoreRadius")]
    public float ChillCoreRadius = 1f;

    /// <summary>
    /// The radius around the user that this ability affects with a chance of happening
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("chillFalloffRadius")]
    public float ChillFalloffRadius = 5f;

    /// <summary>
    /// The chance the chillFalloffRadius happens. 1 is always 0 is never
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("chillFalloffChance")]
    public float ChillFalloffChance = .5f;

    /// <summary>
    /// The amount of frezon (in moles) added to a tile's atmosphere for every
    /// tile that was frozen by the ability
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("chillFrezonPerTile")]
    public float ChillFrezonPerTile = 2f;

    #endregion
}
