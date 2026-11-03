using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Plumbing;

/// <summary>
/// Sent when a reagent is picked from a smart dispenser's context menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserContextDispenseEvent(NetEntity dispenser, string reagentId, FixedPoint2 fallbackAmount)
    : EntityEventArgs
{
    public readonly NetEntity Dispenser = dispenser;

    public readonly string ReagentId = reagentId;

    /// <summary>
    /// Amount for held containers that have no transfer amount of their own.
    /// </summary>
    public readonly FixedPoint2 FallbackAmount = fallbackAmount;
}
