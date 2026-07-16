using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.IdentityManagement;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared._Starlight.Kitchen.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Kitchen.EntitySystems;

/// <summary>
///     Server-side implementation of the sharp/butcher system.
/// </summary>
public sealed partial class SharpSystem : SharedSharpSystem
{
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedDestructibleSystem _destructibleSystem = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    private const float ButcheredItemSpawnRadius = 0.25f;

    /// <summary>
    ///     Subscribes server-specific do-after event handlers.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, SharpDoAfterEvent>(OnDoAfter);
    }

    /// <summary>
    ///     Handles completion of the butcher do-after, spawning products and gibbing/destroying the target entity.
    /// </summary>
    private void OnDoAfter(EntityUid uid, SharpComponent component, DoAfterEvent args)
    {
        if (args.Handled || !TryComp<ButcherableComponent>(args.Args.Target, out var butcher))
            return;

        if (args.Cancelled)
        {
            component.Butchering.Remove(args.Args.Target.Value);
            Dirty(uid, component);
            return;
        }

        component.Butchering.Remove(args.Args.Target.Value);
        Dirty(uid, component);

        var spawnEntities = EntitySpawnCollection.GetSpawns(butcher.SpawnedEntities, _robustRandom);
        var coords = _transform.GetMapCoordinates(args.Args.Target.Value);
        EntityUid popupEnt = default!;

        if (ContainerSystem.TryGetContainingContainer(args.Args.Target.Value, out var container))
        {
            foreach (var proto in spawnEntities)
            {
                // distribute the spawned items randomly in a small radius around the origin
                popupEnt = SpawnInContainerOrDrop(proto, container.Owner, container.ID);
            }
        }
        else
        {
            foreach (var proto in spawnEntities)
            {
                // distribute the spawned items randomly in a small radius around the origin
                popupEnt = Spawn(proto, coords.Offset(_robustRandom.NextVector2(ButcheredItemSpawnRadius)));
            }
        }

        // only show a big popup when butchering living things.
        // Meant to differentiate cutting up clothes and cutting up your boss.
        var popupType = HasComp<MobStateComponent>(args.Args.Target.Value)
            ? PopupType.LargeCaution
            : PopupType.Small;

        PopupSystem.PopupEntity(Loc.GetString("butcherable-knife-butchered-success", ("target", args.Args.Target.Value), ("knife", Identity.Entity(uid, EntityManager))),
            popupEnt,
            args.Args.User,
            popupType);

        _gibbing.Gib(args.Args.Target.Value); // does nothing if ent can't be gibbed
        _destructibleSystem.DestroyEntity(args.Args.Target.Value);

        args.Handled = true;

        _adminLogger.Add(LogType.Gib,
            $"{ToPrettyString(args.User):user} " +
            $"has butchered {ToPrettyString(args.Target):target} " +
            $"with {ToPrettyString(args.Used):knife}");
    }
}
