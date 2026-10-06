using Content.Shared._Starlight.Interaction.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Sound.Components;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Interaction;

/// <summary>
/// Activates worn items marked with <see cref="ActivateOnWearerHuggedComponent"/> when their wearer
/// is hugged. Triggers each item's own activation so its E-press behavior and cooldown are reused.
/// </summary>
public sealed partial class ActivateOnWearerHuggedSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnWearerHugged(Entity<InteractionPopupComponent> ent, ref InteractionSuccessEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        // Exclude pockets from slots that hugs can activate
        if (!_inventory.TryGetContainerSlotEnumerator(ent.Owner, out var enumerator, SlotFlags.WITHOUT_POCKET))
            return;

        // Mirror InteractionPopupSystem's prediction gate. Failed hugs don't trigger effects.
        var predicted = ent.Comp.SuccessChance is 0 or 1
            && ent.Comp.InteractSuccessSpawn == null
            && ent.Comp.InteractFailureSpawn == null;

        while (enumerator.MoveNext(out var container))
        {
            if (container.ContainedEntity is not { } item)
                continue;

            if (!HasComp<ActivateOnWearerHuggedComponent>(item))
                continue;

            ActivateWornItem(item, args.User, predicted);
        }
    }

    private void ActivateWornItem(EntityUid item, EntityUid user, bool predicted)
    {
        // Respect item cooldown
        TryComp<UseDelayComponent>(item, out var delay);
        if (delay != null && _useDelay.IsDelayed((item, delay)))
            return;

        var activateMsg = new ActivateInWorldEvent(user, item, complex: false);
        RaiseLocalEvent(item, activateMsg, broadcast: true);

        if (!activateMsg.Handled)
            return;

        if (delay != null)
            _useDelay.TryResetDelay((item, delay));

        // Fallback: Non-predicted hugs (like on entities with a fail chance) don't play
        // a sound for the client, but everyone else hears it as intended, so play the
        // sound to them directly.
        if (!predicted
            && _net.IsServer
            && TryComp<EmitSoundOnActivateComponent>(item, out var emit)
            && emit.Sound is { } sound)
        {
            _audio.PlayEntity(sound, Filter.Entities(user), item, false);
        }
    }
}
