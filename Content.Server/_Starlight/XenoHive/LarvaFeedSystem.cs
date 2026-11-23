using Content.Shared._Starlight.XenoHive.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Whitelist;

namespace Content.Server._Starlight.XenoHive;

public sealed class LarvaFeedSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LarvaFeedComponent, LarvaFeedDoAfterEvent>(OnFeedDone);
    }

    public bool CanFeed(EntityUid feeder, EntityUid target)
    {
        if (feeder == target || !TryComp<LarvaFeedComponent>(feeder, out var comp))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState) ||
            !comp.TargetStates.Contains(mobState.CurrentState))
            return false;

        if (comp.Whitelist != null && _whitelist.IsWhitelistFailOrNull(comp.Whitelist, target))
            return false;

        return true;
    }

    public bool TryStartFeed(EntityUid feeder, EntityUid target)
    {
        if (!TryComp<LarvaFeedComponent>(feeder, out var comp) || comp.DoAfter != null || !CanFeed(feeder, target))
            return false;

        var args = new DoAfterArgs(EntityManager, feeder, comp.FeedTime, new LarvaFeedDoAfterEvent(), feeder, target)
        {
            DistanceThreshold = comp.DistanceThreshold,
            BreakOnDamage = comp.BreakOnDamage,
            BreakOnMove = comp.BreakOnMove,
            BreakOnWeightlessMove = comp.BreakOnMove,
        };

        if (!_doAfter.TryStartDoAfter(args, out var id))
            return false;

        comp.Target = target;
        comp.DoAfter = id;

        if (comp.StartPopup is { } popup)
            Popup(popup, feeder, target);

        return true;
    }

    /// <summary>Called when the HTN plan is interrupted.</summary>
    public void CancelFeed(EntityUid feeder)
    {
        if (TryComp<LarvaFeedComponent>(feeder, out var comp) && comp.DoAfter is { } id)
            _doAfter.Cancel(id);
    }

    private void OnFeedDone(Entity<LarvaFeedComponent> ent, ref LarvaFeedDoAfterEvent args)
    {
        var comp = ent.Comp;
        comp.Target = null;
        comp.DoAfter = null;

        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanFeed(ent, target))
            return;

        args.Handled = true;

        ApplyToTarget(comp, target, ent);

        if (comp.DonePopup is { } popup)
            Popup(popup, ent, target);

        if (comp.GrowInto is { } proto)
            Spawn(proto, Transform(ent).Coordinates);

        if (comp.DeleteSelf)
            QueueDel(ent);
    }

    private void ApplyToTarget(LarvaFeedComponent comp, EntityUid target, EntityUid feeder)
    {
        // One-time damage, dealt first so everything after sees the final state
        if (comp.Damage != null)
            _damageable.TryChangeDamage(target, comp.Damage, comp.DamageIgnoreResistances, origin: feeder);
        // Add a component to the target
        if (comp.AddToTarget.Count > 0)
            EntityManager.AddComponents(target, comp.AddToTarget, removeExisting: false);
        // Set a rot timer (changeling does this so why not)
        if (comp.RotAfter is { } rot && TryComp<PerishableComponent>(target, out var perishable))
            _rotting.SetRotAfter(target, rot, perishable);
    }

    private void Popup(string locId, EntityUid user, EntityUid target)
    {
        var message = Loc.GetString(locId,
            ("user", Identity.Entity(user, EntityManager)),
            ("target", Identity.Entity(target, EntityManager)));

        _popup.PopupEntity(message, user, PopupType.LargeCaution);
    }
}