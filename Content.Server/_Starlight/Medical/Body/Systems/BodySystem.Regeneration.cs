using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;

namespace Content.Server._Starlight.Medical.Body.Systems;

public sealed partial class BodySystem
{
    /// <summary>
    /// Restores the anatomy described by the body's prototype. Parts grown by this body are healed and retained;
    /// missing parts are regrown, and foreign replacements are detached through the normal limb lifecycle.
    /// </summary>
    /// <remarks>
    /// Native organs are recreated, while grafts, implants, cavity contents and organs carrying a mind are ejected.
    /// Origin is tracked independently of species, so a same-species graft is still a foreign replacement.
    /// </remarks>
    /// <returns>
    /// True when restoration completes or the body has no restorable template. False if attachment or detachment
    /// fails; changes already made are not rolled back.
    /// </returns>
    public bool TryRestoreBody(Entity<BodyComponent?> body)
    {
        if (!Resolve(body, ref body.Comp, logMissing: false) || body.Comp.Prototype is not { } prototypeId)
            return true;

        var prototype = Prototypes.Index(prototypeId);
        if (prototype.Slots[prototype.Root].Part is not { } rootPrototype)
            return true;

        TryComp<HumanoidAppearanceComponent>(body, out var appearance);
        var root = GetRootPartOrNull(body, body.Comp);
        if (root is { } oldRoot && oldRoot.BodyPart.OriginalBody != body.Owner)
        {
            // Remove the root's children through surgery to clean up their hands and limb abilities.
            foreach (var (uid, part) in GetBodyChildren(body)
                         .Where(part => GetParentPartOrNull(part.Id) == oldRoot.Entity).ToArray())
            {
                if (!TryShedPart((body.Owner, body.Comp), (uid, part), appearance))
                    return false;
            }

            if (!Containers.Remove(oldRoot.Entity, body.Comp.RootContainer, destination: Transform(body).Coordinates))
                return false;
            root = null;
        }

        if (root == null)
        {
            var uid = Spawn(rootPrototype, Transform(body).Coordinates);
            var part = Comp<BodyPartComponent>(uid);
            part.OriginalBody = body.Owner;
            if (!AttachPartToRoot(body, uid, body.Comp, part))
            {
                QueueDel(uid);
                return false;
            }
            root = (uid, part);
        }

        var visited = new HashSet<string> { prototype.Root };
        var pending = new Queue<(string Slot, Entity<BodyPartComponent> Part)>();
        pending.Enqueue((prototype.Root, (root.Value.Entity, root.Value.BodyPart)));

        while (pending.TryDequeue(out var current))
        {
            var template = prototype.Slots[current.Slot];
            if (TryComp<DamageableComponent>(current.Part, out var damage))
                Damageable.SetAllDamage((current.Part.Owner, damage), 0);
            if (!TryRestoreOrgans(body, current.Part, template.Organs))
                return false;

            foreach (var connection in template.Connections)
            {
                if (!visited.Add(connection) || prototype.Slots[connection].Part is not { } partPrototype)
                    continue;

                Entity<BodyPartComponent>? child = null;
                if (Containers.TryGetContainer(current.Part, GetPartSlotContainerId(connection), out var container))
                {
                    foreach (var uid in container.ContainedEntities.ToArray())
                    {
                        var part = Comp<BodyPartComponent>(uid);
                        // Keep native limbs in place so repeated restoration does not shed healthy anatomy.
                        if (part.OriginalBody == body.Owner)
                            child = (uid, part);
                        else if (!TryShedPart((body.Owner, body.Comp), (uid, part), appearance))
                            return false;
                    }
                }

                if (child == null)
                {
                    var uid = Spawn(partPrototype, Transform(body).Coordinates);
                    var part = Comp<BodyPartComponent>(uid);
                    part.OriginalBody = body.Owner;
                    child = (uid, part);
                    TryCreatePartSlot(current.Part, connection, part.PartType, out _, current.Part.Comp);
                    var attached = appearance != null
                        ? _limbSystem.AttachLimb((body, appearance), connection, current.Part, child.Value)
                        : AttachPart(current.Part, connection, uid, current.Part.Comp, part);
                    if (!attached)
                    {
                        QueueDel(uid);
                        return false;
                    }
                }

                pending.Enqueue((connection, child.Value));
            }
        }

        body.Comp.LegEntities.RemoveWhere(uid => !TryComp<BodyPartComponent>(uid, out var part) || part.Body != body.Owner);
        UpdateMovementSpeed(body, body.Comp);
        return true;
    }

    /// <summary>
    /// Detaches a replacement without rebuilding its contents. Normal limb removal updates hands, visuals and
    /// the body membership of contained organs, allowing their effects to be removed from the former body.
    /// </summary>
    private bool TryShedPart(Entity<BodyComponent> body, Entity<BodyPartComponent> part,
        HumanoidAppearanceComponent? appearance)
        => appearance != null
            ? _limbSystem.Amputatate((body, Transform(body), appearance, body.Comp),
                (part, Transform(part), MetaData(part), part.Comp))
            : Containers.TryGetContainingContainer(part.Owner, out var container)
                && Containers.Remove(part.Owner, container, destination: Transform(body).Coordinates);

    /// <summary>
    /// Replaces native organs with their template defaults and ejects other contents from this part.
    /// An organ's origin does not establish ownership of its mind, so any organ carrying a mind is preserved.
    /// </summary>
    private bool TryRestoreOrgans(EntityUid body, Entity<BodyPartComponent> part, Dictionary<string, string> organs)
    {
        // Visit every registered slot, including implants and cavity items absent from the native template.
        foreach (var slotId in part.Comp.Organs.Keys.ToArray())
        {
            if (!Containers.TryGetContainer(part, GetOrganContainerId(slotId), out var container))
                continue;

            foreach (var uid in container.ContainedEntities.ToArray())
            {
                if (TryComp<OrganComponent>(uid, out var organ) && organ.OriginalBody == body
                    && !_mindSystem.TryGetMind(uid, out _, out _))
                    // Synchronous deletion frees the slot and marks the organ as terminating before removal
                    // handlers run. Native Diona organs must not turn into nymphs during regeneration.
                    Del(uid);
                else if (!Containers.Remove(uid, container, destination: Transform(body).Coordinates))
                    return false;
            }
        }

        foreach (var (slotId, organPrototype) in organs)
        {
            TryCreateOrganSlot(part, slotId, out _, part.Comp);
            if (organPrototype == "null")
                continue;

            var uid = Spawn(organPrototype, Transform(body).Coordinates);
            var organ = Comp<OrganComponent>(uid);
            organ.OriginalBody = body;
            if (!InsertOrgan(part, uid, slotId, part.Comp, organ))
            {
                QueueDel(uid);
                return false;
            }
        }

        return true;
    }
}
