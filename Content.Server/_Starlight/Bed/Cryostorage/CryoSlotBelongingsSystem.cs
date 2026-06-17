using Content.Shared.Administration.Logs;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Bed.Cryostorage;

// keeps the belongings of players who go into cryo and hands them to whoever
// takes over the job slot they freed up, as long as the items are still around
public sealed partial class CryoSlotBelongingsSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // the bag we hand to whoever takes a cryo vacated slot
    private static readonly EntProtoId CryoBelongingsBag = "ClothingBackpackDuffelCryostorageBelongings";

    // preserved belongings keyed by the job slot they freed up
    private readonly Dictionary<ProtoId<JobPrototype>, Queue<CryoLoadout>> _preservedLoadouts = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CryoSlotReturnedEvent>(OnSlotReturned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnAnyPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _preservedLoadouts.Clear();
    }

    // snapshot the cryo'd player's stuff so we can return it on their slot
    private void OnSlotReturned(ref CryoSlotReturnedEvent args)
    {
        var items = CollectCryoItems(args.Body);
        if (items.Count == 0)
            return;

        var loadout = new CryoLoadout
        {
            Body = args.Body,
            Pod = CompOrNull<CryostorageContainedComponent>(args.Body)?.Cryostorage,
            Items = items,
        };

        if (!_preservedLoadouts.TryGetValue(args.Job, out var queue))
        {
            queue = new Queue<CryoLoadout>();
            _preservedLoadouts[args.Job] = queue;
        }

        queue.Enqueue(loadout);
    }

    // late joiners on a preserved slot spawn at cryo and get a bag of the old stuff
    private void OnAnyPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!args.LateJoin || args.JobId == null)
            return;

        ProtoId<JobPrototype> job = args.JobId;
        if (!_preservedLoadouts.TryGetValue(job, out var queue))
            return;

        while (queue.Count > 0)
        {
            var loadout = queue.Dequeue();
            if (Deleted(loadout.Body))
                continue;

            var valid = GetValidItems(loadout);
            if (valid.Count == 0)
                continue;

            GiveCryoBag(args.Mob, loadout, valid);
            break;
        }

        if (queue.Count == 0)
            _preservedLoadouts.Remove(job);
    }
}
