using Content.Shared._Starlight.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Chemistry.Components;

/// <summary>
/// A machine that puts an empty medipen's original dose back into it, paid for out of the containers loaded into it.
/// </summary>
[RegisterComponent]
[Access(typeof(MedipenRefillerSystem))]
public sealed partial class MedipenRefillerComponent : Component
{
    /// <summary>
    /// The item slots holding the containers a refill is paid from, in the order they're drawn on.
    /// </summary>
    [DataField]
    public List<string> Slots = new();

    /// <summary>
    /// The solution on a pen that holds its dose.
    /// </summary>
    [DataField]
    public string PenSolution = "hypospray";

    /// <summary>
    /// How long refilling a pen takes.
    /// </summary>
    [DataField]
    public TimeSpan RefillDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The pens this machine can refill.
    /// </summary>
    [DataField]
    public List<EntProtoId> Pens = new();

    /// <summary>
    /// Reagents some pens only carry for their sprite. They're put back without coming out of the tank.
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> FreeReagents = new();
}

[Serializable, NetSerializable]
public enum MedipenRefillerVisuals : byte
{
    Refilling,
}

[Serializable, NetSerializable]
public sealed partial class MedipenRefillDoAfterEvent : SimpleDoAfterEvent;
