using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry.Components;

/// <summary>
/// A machine that dispenses reagents into a solution container from containers in its storage slots.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedReagentDispenserSystem))]
public sealed partial class ReagentDispenserComponent : Component
{
    [DataField]
    public ItemSlot BeakerSlot = new();

    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    [DataField, AutoNetworkedField]
    public ReagentDispenserDispenseAmount DispenseAmount = ReagentDispenserDispenseAmount.U10;

    #region Starlight

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> GeneratableReagents = new();

    [DataField]
    public LocId? NoEnergyPopup = "reagent-dispenser-popup-no-energy";
    #endregion
}
