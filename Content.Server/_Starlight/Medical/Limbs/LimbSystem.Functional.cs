using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Limbs;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;

namespace Content.Server._Starlight.Medical.Limbs;
public sealed partial class LimbSystem : SharedLimbSystem
{
    private void AddLimb(Entity<HumanoidAppearanceComponent> body, Entity<SLBodyPartComponent> limb)
    {
        if (TryComp<SLBodyPartComponent>(limb, out var slLimb))
        {
            foreach (var part in _slBody.GetSubtree((limb, slLimb)))
            {
                if (part.Owner != limb.Owner
                    && TryComp(part, out SLBodyPartComponent? inner)
                    && inner.Kind is BodyPartType.Hand or BodyPartType.Foot)
                    AddLimbVisual(body, (part, inner));
            }
        }

        RaiseLimbAttachedEvent(body, limb);
    }

    private void RaiseLimbAttachedEvent(Entity<HumanoidAppearanceComponent> body, Entity<SLBodyPartComponent> limb)
    {
        var @event = new LimbAttachedEvent
        {
            Limb = limb,
            Body = body
        };
        RaiseLocalEvent(body, ref @event);
        RaiseLocalEvent(limb, ref @event);
    }

    private void RemoveLimb(Entity<TransformComponent, HumanoidAppearanceComponent, SLBodyComponent> body, Entity<TransformComponent, MetaDataComponent, SLBodyPartComponent> limb)
    {
        RaiseLimbPreDetachEvent(body, limb);

        // Organs held by hands of the removed subtree (implants) leave with it.
        if (TryComp<SLBodyPartComponent>(limb, out var slLimb))
        {
            foreach (var part in _slBody.GetSubtree((limb, slLimb)))
            {
                // Items attached in place of a hand or a foot drop instead of staying stuck to the body.
                if (part.Owner != limb.Owner
                    && TryComp(part, out CustomLimbComponent? customLimb)
                    && part.Comp.ParentSocket is { } socket)
                {
                    AmputateItemLimb(body, (part, Transform(part), MetaData(part), part.Comp),
                        SharedBodySystem.GetLegacySlotName(socket.SocketId), customLimb);
                    continue;
                }

                if (!HasComp<OrganComponent>(part)
                    || part.Comp.Parent is not { } holder
                    || CompOrNull<SLBodyPartComponent>(holder)?.Kind != BodyPartType.Hand)
                    continue;

                var ev = new SurgeryOrganExtracted(body, holder, part);
                RaiseLocalEvent(part, ref ev);
            }
        }

        RaiseLimbDetachedEvent(body, limb);
    }

    private void RaiseLimbDetachedEvent(
        Entity<TransformComponent, HumanoidAppearanceComponent, SLBodyComponent> body,
        Entity<TransformComponent, MetaDataComponent, SLBodyPartComponent> limb)
    {
        var @event = new LimbDetachedEvent
        {
            Limb = limb,
            Body = body
        };
        RaiseLocalEvent(body, ref @event);
        RaiseLocalEvent(limb, ref @event);
    }
    private void RaiseLimbPreDetachEvent(
        Entity<TransformComponent, HumanoidAppearanceComponent, SLBodyComponent> body,
        Entity<TransformComponent, MetaDataComponent, SLBodyPartComponent> limb)
    {
        var @event = new LimbPreDetachEvent
        {
            Limb = limb,
            Body = body
        };
        RaiseLocalEvent(body, ref @event);
        RaiseLocalEvent(limb, ref @event);
    }
}
