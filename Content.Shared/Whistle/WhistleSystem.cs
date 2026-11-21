using Content.Shared.Actions; // Starlight-edit
using Content.Shared.Coordinates;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory; // Starlight-edit
using Content.Shared.Stealth.Components;
using Content.Shared.Timing; // Starlight-edit
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared.Whistle;

public sealed partial class WhistleSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UseDelaySystem _useDelay = default!; // Starlight-edit

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WhistleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<WhistleComponent, GetItemActionsEvent>(OnGetActions); // Starlight-edit
        SubscribeLocalEvent<WhistleComponent, WhistleActionEvent>(OnWhistleAction); // Starlight-edit
    }

    // Starlight start
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

        // ensures the whistle cooldown triggers from both in-hand and action key activation
        if (TryComp<UseDelayComponent>(uid, out var delay))
        {
            if (_useDelay.IsDelayed((uid, delay)))
                return;
        }


        var useEv = new UseInHandEvent(args.Performer);
        RaiseLocalEvent(uid, useEv);
        if (!useEv.Handled)
            return;


        if (delay != null)
            _useDelay.TryResetDelay((uid, delay));

        args.Handled = true;
    }
    // Starlight end

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

// Starlight-edit
public sealed partial class WhistleActionEvent : InstantActionEvent;
