using System.Linq;
using Content.Shared._Starlight.Humanoid;
using Content.Shared.Clothing;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Humanoid;

/// <summary>
/// Ensures every Neocyte has a frame while preserving frames selected by normal and antagonist loadouts.
/// </summary>
public sealed partial class NeocyteSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStationSpawningSystem _stationSpawning = default!;

    private readonly HashSet<EntityUid> _pendingFrameChecks = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NeocyteComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NeocyteComponent, StartingGearEquippedEvent>(OnStartingGearEquipped,
            after: [typeof(LoadoutSystem)]);
        SubscribeLocalEvent<NeocyteComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingFrameChecks.Count == 0)
            return;

        var pendingFrameChecks = _pendingFrameChecks.ToArray();
        _pendingFrameChecks.Clear();

        foreach (var uid in pendingFrameChecks)
        {
            if (Deleted(uid) || !TryComp(uid, out NeocyteComponent? component))
                continue;

            EnsureFrame((uid, component));
        }
    }

    private void OnMapInit(Entity<NeocyteComponent> entity, ref MapInitEvent args) =>
        // Station, antag, and admin spawning apply their loadouts after MapInit. Waiting until Update lets those
        // explicit choices win while still covering entities spawned directly from their prototype.
        _pendingFrameChecks.Add(entity);

    private void OnStartingGearEquipped(Entity<NeocyteComponent> entity, ref StartingGearEquippedEvent args)
    {
        _pendingFrameChecks.Remove(entity);
        EnsureFrame(entity);
    }

    private void OnShutdown(Entity<NeocyteComponent> entity, ref ComponentShutdown args) => _pendingFrameChecks.Remove(entity);

    private void EnsureFrame(Entity<NeocyteComponent> entity)
    {
        if (_inventory.TryGetSlotEntity(entity, entity.Comp.FrameSlot, out _))
            return;

        if (!_prototypeManager.TryIndex(entity.Comp.FrameLoadoutGroup, out var frameGroup))
        {
            Log.Error($"Neocyte {ToPrettyString(entity)} has an invalid frame loadout group: {entity.Comp.FrameLoadoutGroup}");
            return;
        }

        var validFrames = new List<string>();
        foreach (var loadoutId in frameGroup.Loadouts)
        {
            if (_prototypeManager.TryIndex(loadoutId, out var loadout) &&
                TryGetGearForSlot(loadout, entity.Comp.FrameSlot, out var framePrototype))
                validFrames.Add(framePrototype);
        }

        if (validFrames.Count == 0)
        {
            Log.Error($"Neocyte frame loadout group {frameGroup.ID} has no loadouts for slot {entity.Comp.FrameSlot}");
            return;
        }

        var selectedFrame = _random.Pick(validFrames);
        if (!_inventory.SpawnItemInSlot(
                entity,
                entity.Comp.FrameSlot,
                selectedFrame,
                silent: true,
                force: true))
        {
            Log.Error($"Failed to equip fallback Neocyte frame {selectedFrame} to {ToPrettyString(entity)}");
        }
    }

    /// <summary>
    /// Applies a freshly spawned antagonist's normal species loadout, except for its frame group when the
    /// antagonist loadout explicitly supplies a frame of its own.
    /// </summary>
    public void EquipSpeciesLoadoutForAntag(
        EntityUid uid,
        HumanoidCharacterProfile? profile,
        ICommonSession? session,
        RoleLoadout? antagLoadout,
        ProtoId<StartingGearPrototype>? antagStartingGear)
    {
        if (profile == null || !TryComp(uid, out NeocyteComponent? component))
            return;

        if (!_prototypeManager.TryIndex(profile.Species, out SpeciesPrototype? species) ||
            species.Loadout == null ||
            !_prototypeManager.TryIndex(species.Loadout.Value, out RoleLoadoutPrototype? speciesLoadoutPrototype))
        {
            return;
        }

        var speciesLoadout = profile.GetSpeciesLoadoutOrDefault(session, _prototypeManager)?.Clone();
        if (speciesLoadout == null)
            return;

        speciesLoadout.Role = species.Loadout.Value;

        if (ProvidesGearForSlot(antagLoadout, component.FrameSlot) ||
            ProvidesGearForSlot(antagStartingGear, component.FrameSlot))
        {
            speciesLoadout.SelectedLoadouts.Remove(component.FrameLoadoutGroup);
        }

        if (speciesLoadout.SelectedLoadouts.All(group => group.Value.Count == 0))
            return;

        _stationSpawning.EquipRoleLoadout(uid, speciesLoadout, speciesLoadoutPrototype, profile);
    }

    private bool ProvidesGearForSlot(RoleLoadout? roleLoadout, string slot)
    {
        if (roleLoadout == null)
            return false;

        foreach (var selectedLoadouts in roleLoadout.SelectedLoadouts.Values)
        {
            foreach (var selectedLoadout in selectedLoadouts)
            {
                if (_prototypeManager.TryIndex(selectedLoadout.Prototype, out var loadout) &&
                    ProvidesGearForSlot(loadout, slot))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ProvidesGearForSlot(ProtoId<StartingGearPrototype>? startingGear, string slot) => _prototypeManager.Resolve(startingGear, out StartingGearPrototype? gear) &&
               !string.IsNullOrEmpty(GetGearForSlot(gear, slot));

    private bool ProvidesGearForSlot(LoadoutPrototype loadout, string slot) => TryGetGearForSlot(loadout, slot, out _);

    private bool TryGetGearForSlot(LoadoutPrototype loadout, string slot, out string gear)
    {
        gear = GetGearForSlot(loadout, slot);
        if (!string.IsNullOrEmpty(gear))
            return true;

        if (_prototypeManager.Resolve(loadout.StartingGear, out StartingGearPrototype? startingGear))
            gear = GetGearForSlot(startingGear, slot);

        return !string.IsNullOrEmpty(gear);
    }

    private static string GetGearForSlot(IEquipmentLoadout equipment, string slot) => equipment.Equipment.TryGetValue(slot, out var gear)
            ? gear
            : string.Empty;
}
