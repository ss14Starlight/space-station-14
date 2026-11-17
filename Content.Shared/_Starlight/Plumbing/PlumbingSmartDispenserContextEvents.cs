using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Plumbing;

/// <summary>
/// Sent when a reagent is picked from a smart dispenser's context menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserContextDispenseEvent(
    NetEntity dispenser,
    ReagentId reagentId,
    ReagentDispenserDispenseAmount fallbackAmount)
    : EntityEventArgs
{
    public readonly NetEntity Dispenser = dispenser;

    public readonly ReagentId ReagentId = reagentId;

    /// <summary>
    /// Amount selected in the dispenser window, for held containers that have no transfer amount of their own.
    /// </summary>
    public readonly ReagentDispenserDispenseAmount FallbackAmount = fallbackAmount;
}
