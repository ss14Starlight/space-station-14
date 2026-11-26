using Content.Shared._Starlight.Containers;
using Content.Shared.Atmos;
using Content.Shared.Temperature;
using Robust.Shared.Containers;

// ReSharper disable CheckNamespace
namespace Content.Server._Starlight.Containers;

public sealed partial class FireproofContainerSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containerSystem = null!;

    [SubscribeLocalEvent]
    private void OnContainerStartup(EntityUid uid, FireproofContainerComponent component, ComponentStartup args) =>
        ReconcileContents(uid);

    [SubscribeLocalEvent]
    private void OnContainerMapInit(EntityUid uid, FireproofContainerComponent component, MapInitEvent args) =>
        ReconcileContents(uid);

    [SubscribeLocalEvent]
    private void OnContainerShutdown(EntityUid uid, FireproofContainerComponent component, ComponentShutdown args) =>
        ReconcileContents(uid, uid);

    [SubscribeLocalEvent]
    private void OnFireproofContainerInserted(EntityUid uid, FireproofContainerComponent component,
        EntInsertedIntoContainerMessage args) =>
        ReconcileEntityAndContents(args.Entity);

    [SubscribeLocalEvent]
    private void OnFireproofContainerRemoved(EntityUid uid, FireproofContainerComponent component,
        EntRemovedFromContainerMessage args) =>
        ReconcileEntityAndContents(args.Entity, uid);

    [SubscribeLocalEvent]
    private void OnFireproofContentsInserted(EntityUid uid, FireproofContentsComponent component,
        EntInsertedIntoContainerMessage args) =>
        ReconcileEntityAndContents(args.Entity);

    [SubscribeLocalEvent]
    private void OnFireproofContentsRemoved(EntityUid uid, FireproofContentsComponent component,
        EntRemovedFromContainerMessage args) =>
        ReconcileEntityAndContents(args.Entity, uid);

    [SubscribeLocalEvent]
    private void OnFireproofContentsParentChanged(Entity<FireproofContentsComponent> ent,
        ref EntParentChangedMessage args) =>
        ReconcileEntityAndContents(ent.Owner);

    [SubscribeLocalEvent]
    private void OnGetFireProtection(Entity<FireproofContentsComponent> ent, ref GetFireProtectionEvent args)
    {
        if (IsInsideFireproofContainer(ent.Owner))
            args.Reduce(1f);
    }

    [SubscribeLocalEvent]
    private void OnModifyTemperature(EntityUid uid, FireproofContentsComponent component,
        ModifyChangedTemperatureEvent args)
    {
        if (args.TemperatureDelta > 0f && IsInsideFireproofContainer(uid))
            args.TemperatureDelta = 0f;
    }

    private void ReconcileContents(EntityUid uid, EntityUid? ignoredContainer = null)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
                ReconcileEntityAndContents(contained, ignoredContainer);
        }
    }

    private void ReconcileEntityAndContents(EntityUid root, EntityUid? ignoredContainer = null)
    {
        var pending = new Queue<EntityUid>();
        pending.Enqueue(root);

        while (pending.TryDequeue(out var uid))
        {
            if (!Exists(uid))
                continue;

            var protectedByContainer = false;
            foreach (var container in _containerSystem.GetContainingContainers((uid, Transform(uid))))
            {
                if (container.Owner == ignoredContainer || !HasComp<FireproofContainerComponent>(container.Owner))
                    continue;

                protectedByContainer = true;
                break;
            }

            if (protectedByContainer)
                EnsureComp<FireproofContentsComponent>(uid);
            else
                RemComp<FireproofContentsComponent>(uid);

            if (!TryComp<ContainerManagerComponent>(uid, out var manager))
                continue;

            foreach (var container in manager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                    pending.Enqueue(contained);
            }
        }
    }

    private bool IsInsideFireproofContainer(EntityUid uid)
    {
        foreach (var container in _containerSystem.GetContainingContainers((uid, Transform(uid))))
        {
            if (HasComp<FireproofContainerComponent>(container.Owner))
                return true;
        }

        return false;
    }
}
