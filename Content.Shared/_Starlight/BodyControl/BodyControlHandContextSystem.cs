using System.Linq;
using Content.Shared.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.BodyControl;

/// <summary>Supplies an explicit hand only while a trusted controller command is executing.</summary>
public sealed partial class BodyControlHandContextSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    private readonly Dictionary<EntityUid, Context> _commands = new();

    public sealed record Context(EntityUid Controller, BodyControlAccess Access, string? Hand);

    public void Execute(EntityUid body, EntityUid controller, BodyControlAccess access, string? hand, Action command)
    {
        _commands.Remove(body, out var previous);
        _commands[body] = new(controller, access, hand);
        try { command(); }
        finally
        {
            _commands.Remove(body);
            if (previous != null)
                _commands[body] = previous;
        }
    }

    public bool IsExecuting(EntityUid body) => _commands.ContainsKey(body);

    public bool AllowsObserverInteraction(EntityUid actor)
        => _commands.Values.Any(context => context.Controller == actor && context.Access.Allows("body.interact"));

    public bool TryGetContext(EntityUid body, out Context context)
    {
        if (_commands.TryGetValue(body, out context!))
            return true;
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
        if (!access.Allows("body.hands") || !TryComp<HandsComponent>(body, out var hands) || !hands.Hands.ContainsKey(hand))
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
        => !_commands.TryGetValue(body, out var context) || AllowsHand(body, hand, context.Access);

    public string? GetSelectedHand(EntityUid body, string? fallback)
    {
        if (!TryGetContext(body, out var context))
            return fallback;
        return context.Hand != null && AllowsHand(body, context.Hand, context.Access) ? context.Hand : null;
    }

    public bool IsDoAfterHandValid(EntityUid body, NetEntity controller, string? hand, bool checkSelection)
    {
        return hand != null && TryGetEntity(controller, out var actor)
               && TryComp<BodyControlSourceComponent>(actor, out var source) && source.Body == body
               && (!checkSelection || source.SelectedHand == hand) && AllowsHand(body, hand, source.Access);
    }

    [SubscribeLocalEvent]
    private void OnNativeMelee(Entity<BodyControlSourceComponent> ent, ref GetMeleeWeaponEvent args)
    {
        if (!ent.Comp.ExternallyDriven && !_commands.ContainsKey(ent.Owner)
            && (ent.Comp.Body != ent.Owner || !ent.Comp.Access.FullControl))
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
        if (_commands.ContainsKey(body) && TryComp<HandsComponent>(body, out var hands)
            && hands.Hands.ContainsKey(args.Container.ID) && !AllowsHand(body, args.Container.ID))
            args.Cancel();
    }
}
