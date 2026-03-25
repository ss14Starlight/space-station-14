using Content.Shared.Actions;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Examine;
using Content.Shared._Starlight.Plasmaman.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Plasmaman.EntitySystems;

public abstract partial class SharedSelfExtinguisherSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SelfExtinguisherComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SelfExtinguisherComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<SelfExtinguisherComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<SelfExtinguisherComponent> ent, ref MapInitEvent args)
    {
        if (!_actionContainer.EnsureAction(ent.Owner, ref ent.Comp.ActionEntity, out _, ent.Comp.Action))
            return;

        _actions.SetUseDelay(ent.Comp.ActionEntity, ent.Comp.Cooldown);
    }

    private void OnGetActions(Entity<SelfExtinguisherComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.ActionEntity == null || args.InHands)
            return;

        args.AddAction(ent.Comp.ActionEntity.Value);
    }

    private void OnExamined(Entity<SelfExtinguisherComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<LimitedChargesComponent>(ent.Owner, out var charges) &&
            _charges.IsEmpty((ent.Owner, charges)))
        {
            args.PushMarkup(Loc.GetString("self-extinguisher-examine-no-charges"));
            return;
        }

        var curTime = _timing.CurTime;
        if (ent.Comp.NextExtinguish > curTime)
        {
            args.PushMarkup(Loc.GetString("self-extinguisher-examine-cooldown-recharging",
                ("cooldown", Math.Ceiling((ent.Comp.NextExtinguish - curTime).TotalSeconds))));
            return;
        }

        args.PushMarkup(Loc.GetString("self-extinguisher-examine-cooldown-ready"));
    }

    protected bool SetPopupCooldown(Entity<SelfExtinguisherComponent> ent, TimeSpan? curTime = null)
    {
        curTime ??= _timing.CurTime;

        if (curTime < ent.Comp.NextPopup)
            return false;

        ent.Comp.NextPopup = curTime + ent.Comp.PopupCooldown;
        return true;
    }
}

public sealed partial class SelfExtinguishEvent : InstantActionEvent
{
}
