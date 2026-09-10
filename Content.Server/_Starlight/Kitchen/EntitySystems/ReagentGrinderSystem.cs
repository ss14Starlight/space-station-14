using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Power.EntitySystems;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Kitchen.EntitySystems;

/// <summary>
/// Supplies server-side BUI state while shared predicted grinder behavior lives in the upstream system.
/// </summary>
[UsedImplicitly]
internal sealed class ReagentGrinderSystem : SharedReagentGrinderSystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void UpdateUi(EntityUid uid)
    {
        if (!TryComp<ReagentGrinderComponent>(uid, out var grinder))
            return;

        var output = _itemSlots.GetItemOrNull(uid, ReagentGrinderComponent.BeakerSlotId);
        Solution? outputSolution = null;
        var hasInput = grinder.InputContainer.ContainedEntities.Count > 0;
        var canGrind = false;
        var canJuice = false;

        if (output is not null &&
            _solutions.TryGetFitsInDispenser(output.Value, out _, out outputSolution) &&
            hasInput)
        {
            canGrind = grinder.InputContainer.ContainedEntities.All(entity => CanGrind(entity));
            canJuice = grinder.InputContainer.ContainedEntities.All(entity => CanJuice(entity));
        }

        _ui.SetUiState(uid,
            ReagentGrinderUiKey.Key,
            new ReagentGrinderInterfaceState(
                IsActive((uid, grinder)),
                output.HasValue,
                !grinder.NeedsPower || _power.IsPowered(uid),
                canJuice,
                canGrind,
                grinder.AutoMode,
                GetNetEntityArray(grinder.InputContainer.ContainedEntities.ToArray()),
                outputSolution?.Contents.ToArray()));
    }
}
