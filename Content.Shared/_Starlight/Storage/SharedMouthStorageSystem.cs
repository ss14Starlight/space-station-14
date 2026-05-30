using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared._Starlight.Storage;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Storage;

public abstract class SharedMouthStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MouthStorageComponent, MapInitEvent>(OnMouthStorageInit);
        SubscribeLocalEvent<MouthStorageComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<MouthStorageComponent, DisarmedEvent>(OnDisarmed);
        SubscribeLocalEvent<MouthStorageComponent, DamageChangedEvent>(OnDamageModified);
        SubscribeLocalEvent<MouthStorageComponent, ExaminedEvent>(OnExamined);
    }

    protected bool IsMouthBlocked(MouthStorageComponent component)
    {
        if (!TryComp<StorageComponent>(component.MouthId, out var storage))
            return false;

        return storage.Container.ContainedEntities.Count > 0;
    }

    private void OnMouthStorageInit(EntityUid uid, MouthStorageComponent component, MapInitEvent args)
    {
        if (string.IsNullOrWhiteSpace(component.MouthProto))
            return;

        component.Mouth = _containerSystem.EnsureContainer<Container>(uid, MouthStorageComponent.MouthContainerId);
        component.Mouth.ShowContents = false;
        component.Mouth.OccludesLight = false;

        var mouth = Spawn(component.MouthProto, new EntityCoordinates(uid, 0, 0));
        _containerSystem.Insert(mouth, component.Mouth);
        component.MouthId = mouth;

        if (!string.IsNullOrWhiteSpace(component.OpenStorageAction) && component.Action == null)
            _actionsSystem.AddAction(uid, ref component.Action, component.OpenStorageAction, mouth);
    }

    private void OnDowned(EntityUid uid, MouthStorageComponent component, DownedEvent args)
    {
        SpitOutMouth(uid, component);
    }

    private void OnDisarmed(EntityUid uid, MouthStorageComponent component, DisarmedEvent args)
    {
        SpitOutMouth(uid, component);

    }

    private void OnDamageModified(EntityUid uid, MouthStorageComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null
            || !args.DamageIncreased
            || args.DamageDelta.GetTotal() < component.SpitDamageThreshold)
            return;

        SpitOutMouth(uid, component);
    }

    // Other people can see if this person has items in their mouth.
    private void OnExamined(EntityUid uid, MouthStorageComponent component, ExaminedEvent args)
    {
        if (IsMouthBlocked(component))
        {
            var subject = Identity.Entity(uid, EntityManager);
            args.PushMarkup(Loc.GetString("mouth-storage-examine-condition-occupied", ("entity", subject)));
        }
    }

    /// <summary>
    ///     Spit out the contents of your mouth
    /// </summary>
    private void SpitOutMouth(EntityUid uid, MouthStorageComponent component)
    {
        if (component.MouthId == null)
            return;

        if (!TryComp<StorageComponent>(component.MouthId.Value, out var storage) || storage.Container.ContainedEntities.Count == 0)
            return;

        var dumpQueue = new Queue<EntityUid>(storage.Container.ContainedEntities);

        foreach (var entity in dumpQueue)
        {
            _transform.SetCoordinates(entity, Transform(uid).Coordinates);
            _transform.AttachToGridOrMap(entity);
            _throwing.TryThrow(entity, _random.NextVector2());
        }
    }
}
