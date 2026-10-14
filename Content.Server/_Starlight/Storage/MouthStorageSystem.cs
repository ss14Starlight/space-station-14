using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Storage;
using Content.Shared.Actions;
using Content.Shared.Nutrition;
using Content.Shared.Speech;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Storage;

public sealed partial class MouthStorageSystem : SharedMouthStorageSystem
{
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MouthStorageComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<MouthStorageComponent, IngestionAttemptEvent>(OnIngestAttempt);
        SubscribeLocalEvent<MouthStorageComponent, ComponentStartup>(OnMouthStorageStartup);
    }

    private void OnMouthStorageStartup(EntityUid uid, MouthStorageComponent component, ComponentStartup args)
    {
        if (string.IsNullOrWhiteSpace(component.MouthProto))
            return;

        component.Mouth = _containerSystem.EnsureContainer<Container>(uid, MouthStorageComponent.MouthContainerId);
        component.Mouth.ShowContents = false;
        component.Mouth.OccludesLight = false;

        var mouth = Spawn(component.MouthProto, new EntityCoordinates(uid, 0, 0));
        if (!_containerSystem.Insert(mouth, component.Mouth))
        {
            QueueDel(mouth);
            return;
        }
        component.MouthId = mouth;

        if (!string.IsNullOrWhiteSpace(component.OpenStorageAction) && component.Action == null)
            _actionsSystem.AddAction(uid, ref component.Action, component.OpenStorageAction, mouth);

        Dirty(uid, component);
    }

    /// <summary>
    /// Forces you to mumble if you have items in your mouth.
    /// </summary>
    private void OnAccent(EntityUid uid, MouthStorageComponent component, AccentGetEvent args)
    {
        if (IsMouthBlocked(component))
            args.Message = _replacement.ApplyReplacements(args.Message, "mumble");
    }

    /// <summary>
    /// Attempting to eat or drink anything with items in your mouth won't work
    /// </summary>
    private void OnIngestAttempt(EntityUid uid, MouthStorageComponent component, IngestionAttemptEvent args)
    {
        if (!IsMouthBlocked(component))
            return;

        if (!TryComp<StorageComponent>(component.MouthId, out _))
            return;

        // var firstItem = storage.Container.ContainedEntities[0];
        // args.Blocker = firstItem;
        args.Cancelled = true;
    }
}
