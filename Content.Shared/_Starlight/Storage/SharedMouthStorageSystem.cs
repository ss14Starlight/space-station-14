using System.Numerics;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Storage;

public abstract partial class SharedMouthStorageSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MouthStorageComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<MouthStorageComponent, DisarmedEvent>(OnDisarmed);
        SubscribeLocalEvent<MouthStorageComponent, DamageChangedEvent>(OnDamageModified);
        SubscribeLocalEvent<MouthStorageComponent, ExaminedEvent>(OnExamined);
    }

    protected bool IsMouthBlocked(MouthStorageComponent component)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (!TryComp<StorageComponent>(component.MouthId, out var storage) ||
            storage.Container == null)
            return false;

        return storage.Container.ContainedEntities.Count > 0;
    }

    private void OnDowned(EntityUid uid, MouthStorageComponent component, DownedEvent args) =>
        SpitOutMouth(uid, component);

    private void OnDisarmed(EntityUid uid, MouthStorageComponent component, DisarmedEvent args) =>
        SpitOutMouth(uid, component);

    private void OnDamageModified(EntityUid uid, MouthStorageComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null
            || !args.DamageIncreased
            || args.DamageDelta.GetTotal() < component.SpitDamageThreshold)
            return;

        SpitOutMouth(uid, component);
    }

    /// <summary>
    /// Other people can see if this person has items in their mouth.
    /// </summary>
    private void OnExamined(EntityUid uid, MouthStorageComponent component, ExaminedEvent args)
    {
        if (IsMouthBlocked(component))
        {
            var subject = Identity.Entity(uid, EntityManager);
            args.PushMarkup(Loc.GetString("mouth-storage-examine-condition-occupied", ("entity", subject)));
        }
    }

    /// <summary>
    /// Spit out the contents of your mouth.
    /// </summary>
    private void SpitOutMouth(EntityUid uid, MouthStorageComponent component)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (component.MouthId == null ||
            !TryComp<StorageComponent>(component.MouthId.Value, out var storage) ||
            storage.Container == null ||
            storage.Container.ContainedEntities.Count == 0)
            return;

        var dumpQueue = _container.EmptyContainer(storage.Container, true, Transform(uid).Coordinates);
        var rand = _random.GetPredictedRandom(_gameTiming, GetNetEntity(uid).Id);

        foreach (var entity in dumpQueue)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            // Upstream is trying to disincentivize using System.Random instances, which makes sense, except that
            // they provide no way to do predicted randomness and the RobustToolbox PR produces -- you guessed it --
            // System.Random instances.
            var angle = rand.NextAngle().RotateVec(new Vector2(rand.NextFloat(), 0));
#pragma warning restore CS0618 // Type or member is obsolete

            _throwing.TryThrow(entity, angle);
        }
    }
}
