using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Energy.Supermatter;

[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Activated = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 AccHeat = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 AccRadiation = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 AccLighting = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 AccBreak = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 RadiationStability = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Durability = 100f;

    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 LastSendedDurability = 100f;

    ///read only since server sided?
    /// Multiplier for passive regeneration. Healium Zaunker ZXA
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 RegenerationModifier = 1f;

    /// Multiplies SM gas interaction. Nitrium
    /// 1.0 = normal.
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 ReactionModifier = 1f;

    /// Additional resistance against destabilization.
    /// 1.0 = normal. //slowdown the breaking part. Hypernob decreases instability, AntiNob and zaunker make it more instable
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 DestabilizationModifier = 1f;

    /// Extra damage applied by dangerous gases like Zauker.
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 GasDoesDamage = 0f;

}
