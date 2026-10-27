using System.Linq;
using Content.Shared.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.BodyControl;

/// <summary>Resolves each controller's hand without changing the body's shared active hand.</summary>
public sealed partial class BodyControlHandContextSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    private readonly Dictionary<EntityUid, Context> _commands = new();

    public sealed record Context(EntityUid Controller, BodyControlAccess Access, string? Hand, string? Permission = null);

    public void Execute(EntityUid body, EntityUid controller, BodyControlAccess access, string? hand, Action command,
        string? permission = null)
    {
        _commands.Remove(body, out var previous);
        _commands[body] = new(controller, access, hand, permission);
        try { command(); }
        finally
        {
            _commands.Remove(body);
            if (previous != null)
                _commands[body] = previous;
        }
    }

    public bool IsExecuting(EntityUid body) => _commands.ContainsKey(body);

    public EntityUid GetControlledBody(EntityUid actor)
        => TryComp<BodyControlSourceComponent>(actor, out var source) && !source.ExternallyDriven
           && source.LifeStage <= ComponentLifeStage.Running && !TerminatingOrDeleted(source.Body) ? source.Body : actor;

    /// <summary>Preserves the requester's hand while an existing session handler resolves its physical user.</summary>
    public bool WithController(EntityUid actor, string? permission, bool needsHand, Action command)
    {
        if (!TryComp<BodyControlSourceComponent>(actor, out var source) || source.ExternallyDriven
            || source.LifeStage > ComponentLifeStage.Running || TerminatingOrDeleted(source.Body)
            || IsExecuting(source.Body))
            return false;
        var reason = permission != null && !source.Access.Allows(permission) ? permission[5..]
            : needsHand && (source.SelectedHand == null || !AllowsHand(source.Body, source.SelectedHand, source.Access))
                ? "hands" : null;
        if (reason != null)
        {
            var denied = new BodyControlDeniedEvent(reason);
            RaiseLocalEvent(actor, ref denied);
            return true;
        }
        Execute(source.Body, actor, source.Access, source.SelectedHand, command, permission);
        return true;
    }

    public bool AllowsObserverInteraction(EntityUid actor)
        => _commands.Values.Any(context => context.Controller == actor && context.Access.Allows("body.interact"));

    public bool TryGetContext(EntityUid body, out Context context)
    {
        if (_commands.TryGetValue(body, out context!))
            return true;
        var request = new BodyControlHandContextEvent();
        RaiseLocalEvent(body, ref request);
        if (request.Context is { } presented)
        {
            context = presented;
            return true;
        }
        if (TryComp<BodyControlSourceComponent>(body, out var source) && !source.ExternallyDriven && source.Body == body)
        {
            context = new(body, source.Access, source.SelectedHand);
            return true;
        }
        context = default!;
        return false;
    }

    public bool AllowsHand(EntityUid body, string hand, BodyControlAccess access)
    {
        if (!access.Allows("body.hands") || !_hands.TryGetHand(body, hand, out _))
            return false;
        if (access.FullControl)
            return true;
        if (access.Hands != null && !access.Hands.Contains(hand))
            return false;
        if (access.Parts == null)
            return true;
        foreach (var netPart in access.Parts)
        {
            if (!TryGetEntity(netPart, out var allowed) || !TryComp<BodyPartComponent>(allowed, out var part) || part.Body != body)
                continue;
            foreach (var (uid, child) in _body.GetBodyPartChildren(allowed.Value, part))
            {
                if (child.Body == body && child.PartType == BodyPartType.Hand
                    && _body.GetParentPartAndSlotOrNull(uid) is { } slot
                    && SharedBodySystem.GetPartSlotContainerId(slot.Slot) == hand)
                    return true;
            }
        }
        return false;
    }

    public bool AllowsHand(EntityUid body, string hand)
        => !TryGetContext(body, out var context) || AllowsHand(body, hand, context.Access);

    public string? GetSelectedHand(EntityUid body, string? fallback)
    {
        if (!TryGetContext(body, out var context))
            return fallback;
        return context.Hand != null && AllowsHand(body, context.Hand, context.Access) ? context.Hand : null;
    }

    public bool TryGetController(EntityUid body, NetEntity controller, out EntityUid actor,
        out BodyControlSourceComponent source)
    {
        actor = EntityUid.Invalid;
        source = default!;
        if (!TryGetEntity(controller, out var entity) || entity is not { } resolved || TerminatingOrDeleted(resolved)
            || !TryComp(resolved, out source!) || source.ExternallyDriven || source.Body != body
            || source.LifeStage > ComponentLifeStage.Running || TerminatingOrDeleted(body))
            return false;
        actor = resolved;
        return true;
    }

    [SubscribeLocalEvent]
    private void OnNativeMelee(Entity<BodyControlSourceComponent> ent, ref GetMeleeWeaponEvent args)
    {
        if (!ent.Comp.ExternallyDriven && !_commands.ContainsKey(ent.Owner)
            && (ent.Comp.Body != ent.Owner || !ent.Comp.Access.Allows("body.interact")
                || HasComp<HandsComponent>(ent) && (ent.Comp.SelectedHand == null || !AllowsHand(ent, ent.Comp.SelectedHand, ent.Comp.Access))))
        {
            args.Weapon = null;
            args.Handled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnInsert(Entity<BodyControlTargetComponent> ent, ref ContainerIsInsertingAttemptEvent args)
        => CheckHandContainer(ent, args);

    [SubscribeLocalEvent]
    private void OnRemove(Entity<BodyControlTargetComponent> ent, ref ContainerIsRemovingAttemptEvent args)
        => CheckHandContainer(ent, args);

    private void CheckHandContainer(EntityUid body, ContainerAttemptEventBase args)
    {
        if (_commands.ContainsKey(body) && _hands.TryGetHand(body, args.Container.ID, out _)
            && !AllowsHand(body, args.Container.ID))
            args.Cancel();
    }
}

[ByRefEvent]
public struct BodyControlHandContextEvent
{
    public BodyControlHandContextSystem.Context? Context;
}
