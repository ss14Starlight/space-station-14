using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Gibbing;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Storage;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Storage;

public sealed partial class SharedStomachStorageSystem : EntitySystem
{
    private const string StomachStorageAction = "ActionOpenStomachStorage";

    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StomachStorageComponent, BeingGibbedEvent>(OnBeingGibbed);
        SubscribeLocalEvent<StomachStorageComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<StomachPouchComponent, OrganAddedToBodyEvent>(OnPouchAdded);
        SubscribeLocalEvent<StomachPouchComponent, OrganRemovedFromBodyEvent>(OnPouchRemoved);
    }

    private void OnExamined(EntityUid uid, StomachStorageComponent component, ref ExaminedEvent args)
    {
        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        var message = storage.Container.ContainedEntities.Count > 0
            ? "stomach-storage-examine-occupied"
            : "stomach-storage-examine-empty";

        using (args.PushGroup(nameof(StomachStorageComponent), -100))
        {
            args.PushMarkup(Loc.GetString(message, ("entity", Identity.Entity(uid, EntityManager))));
        }
    }

    private void OnBeingGibbed(Entity<StomachStorageComponent> ent, ref BeingGibbedEvent args)
    {
        if (!TryComp<StorageComponent>(ent.Owner, out var storage))
            return;

        foreach (var item in storage.Container.ContainedEntities.ToArray())
        {
            if (_containers.Remove(item, storage.Container, reparent: false, force: true))
                args.Giblets.Add(item);
        }
    }

    private void OnPouchRemoved(Entity<StomachPouchComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (ent.Comp.ActionEntity is {} action &&
            TryComp<ActionsComponent>(args.OldBody, out var actions))
        {
            _actions.RemoveAction((args.OldBody, actions), action);
            _actionContainer.RemoveAction(action);
            ent.Comp.ActionEntity = null;
        }

        if (TryComp<StorageComponent>(args.OldBody, out var storage))
            _containers.EmptyContainer(storage.Container, true, Transform(args.OldBody).Coordinates);
    }

    private void OnPouchAdded(Entity<StomachPouchComponent> ent, ref OrganAddedToBodyEvent args)
    {
        var actions = EnsureComp<ActionsComponent>(args.Body);
        if (!_actions.AddAction(args.Body, ref ent.Comp.ActionEntity, StomachStorageAction, ent.Owner, actions))
            return;

        Dirty(ent);
    }
}
