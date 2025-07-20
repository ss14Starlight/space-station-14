using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Actions.Hailer;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Actions.Hailer;

public sealed class HailerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HailerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HailerComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HailerActionEvent>(OnHailerAction);
    }

    private void OnEquipped(EntityUid uid, HailerComponent component, GotEquippedEvent args)
    {
        _actions.AddAction(args.Equipee, ref component.ActionEntity, component.Action);
    }

    private void OnUnequipped(EntityUid uid, HailerComponent component, GotUnequippedEvent args)
    {
        if (component.ActionEntity != null)
        {
            _actions.RemoveAction(args.Equipee, component.ActionEntity);
            component.ActionEntity = null;
        }
    }

    private void OnHailerAction(HailerActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        HailerComponent? hailerComponent = null;
        EntityUid? hailerEntity = null;
        
        if (_inventory.TryGetSlots(performer, out var slotDefinitions))
        {
            foreach (var slot in slotDefinitions)
            {
                if (_inventory.TryGetSlotEntity(performer, slot.Name, out var item) && 
                    TryComp<HailerComponent>(item, out var comp))
                {
                    hailerComponent = comp;
                    hailerEntity = item;
                    break;
                }
            }
        }

        if (hailerComponent == null || hailerEntity == null)
            return;

        if (TryComp<UseDelayComponent>(hailerEntity.Value, out var useDelay) && 
            _useDelay.IsDelayed((hailerEntity.Value, useDelay)))
            return;

        if (hailerComponent.HailSound != null)
        {
            _audio.PlayPvs(hailerComponent.HailSound, performer);
        }

        if (!string.IsNullOrEmpty(hailerComponent.HailMessage))
        {
            _chat.TrySendInGameICMessage(performer, hailerComponent.HailMessage, InGameICChatType.Speak, false);
        }

        // Set the cooldown on the hailer entity
        _useDelay.SetLength(hailerEntity.Value, TimeSpan.FromSeconds(hailerComponent.CooldownDuration));
        _useDelay.TryResetDelay(hailerEntity.Value);

        args.Handled = true;
    }
}
