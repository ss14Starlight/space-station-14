using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Implants.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    private void AddExplosionCause(QueuedExplosion explosion, EntityUid? cause, EntityUid? internalBody)
    {
        if (cause is not { } causeUid)
            return;

        if (!explosion.Causes.Contains(causeUid))
            explosion.Causes.Add(causeUid);

        if (internalBody is { } body)
            explosion.InternalBodies.Add(body);
    }

    private static bool HasSameInternalBodies(HashSet<EntityUid> internalBodies, EntityUid? internalBody)
        => internalBody is { } body
            ? internalBodies.Count == 1 && internalBodies.Contains(body)
            : internalBodies.Count == 0;

    private EntityUid? FindContainingBody(EntityUid cause)
    {
        while (TryComp(cause, out TransformComponent? transform))
        {
            if (TryComp(cause, out BodyPartComponent? bodyPart) && bodyPart.Body is { } body)
                return body;

            if (TryComp(cause, out OrganComponent? organ) && organ.Body is { } organBody)
                return organBody;

            var parentUid = transform.ParentUid;
            // Body-owned containers also hold external gear, so only anatomy and implant containers count.
            if (TryComp(parentUid, out BodyComponent? bodyComponent)
                && ((bodyComponent.RootContainer is { } rootContainer
                        && rootContainer.Contains(cause))
                    || (TryComp(parentUid, out ImplantedComponent? implanted)
                        && implanted.ImplantContainer.Contains(cause))))
                return parentUid;

            if (parentUid == cause)
                break;

            cause = parentUid;
        }

        return null;
    }
}
