using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Stealth.Components;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared.Whistle;

public sealed partial class WhistleSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WhistleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<WhistleComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<WhistleComponent, WhistleActionEvent>(OnWhistleAction);
    }

    private void OnGetActions(EntityUid uid, WhistleComponent component, GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } slotFlags || !slotFlags.HasFlag(SlotFlags.NECK))
            return;

        args.AddAction(ref component.ActionEntity, component.Action);
    }

    private void OnWhistleAction(EntityUid uid, WhistleComponent component, WhistleActionEvent args)
    {
        if (args.Handled || !_timing.IsFirstTimePredicted)
            return;

        // Reuse the same path as activating the whistle in-hand, so the action works the same whether the whistle is held or just worn on the neck.
        // the item's own UseDelay isn't checked outside of the hand-use flow, so cooldown is  handled by the action's own useDelay
        args.Handled = true;
        RaiseLocalEvent(uid, new UseInHandEvent(args.Performer) { ApplyDelay = false });
    }

    private void ExclamateTarget(EntityUid target, WhistleComponent component)
    {
        SpawnAttachedTo(component.Effect, target.ToCoordinates());
    }

    public void OnUseInHand(EntityUid uid, WhistleComponent component, UseInHandEvent args)
    {
        if (args.Handled || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = TryMakeLoudWhistle(uid, args.User, component);
    }

    public bool TryMakeLoudWhistle(EntityUid uid, EntityUid owner, WhistleComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || component.Distance <= 0)
            return false;

        MakeLoudWhistle(uid, owner, component);
        return true;
    }

    private void MakeLoudWhistle(EntityUid uid, EntityUid owner, WhistleComponent component)
    {
        StealthComponent? stealth = null;

        foreach (var iterator in
            _entityLookup.GetEntitiesInRange<HumanoidAppearanceComponent>(_transform.GetMapCoordinates(uid), component.Distance))
        {
            //Avoid pinging invisible entities
            if (TryComp(iterator, out stealth) && stealth.Enabled)
                continue;

            //We don't want to ping user of whistle
            if (iterator.Owner == owner)
                continue;

            ExclamateTarget(iterator, component);
        }
    }
}

public sealed partial class WhistleActionEvent : InstantActionEvent;
