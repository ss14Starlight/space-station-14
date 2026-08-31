using Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation
using Content.Shared._Goobstation.StationRadio.Events; // Starlight - _Goob -> _Goobstation
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Communications;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared._Goobstation.StationRadio.Systems;
using Content.Shared._Starlight.StationRadio.Events;

namespace Content.Server._Goobstation.StationRadio; // Starlight - _Goob -> _Goobstation

/// <summary>
/// System that handles spawning game rules when vinyl disks finish playing.
/// </summary>
public sealed partial class VinylSummonRuleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private readonly StationRadioReceiverSystem _stationRadio = default!; // Starlight - Station Radio Check oved to StationRadioReceiverSystem

    private record struct TrackingData(EntityUid VinylPlayerUid, TimeSpan EndTime);
    private readonly Dictionary<EntityUid, TrackingData> _trackingVinyls = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VinylPlayerComponent, VinylInsertedEvent>(OnVinylInserted);
        SubscribeLocalEvent<VinylPlayerComponent, VinylRemovedEvent>(OnVinylRemoved);
        SubscribeLocalEvent<VinylSummonRuleComponent, VinylFinishedEvent>(OnVinylFinished);//Starlight: Eventify vinyl finishing.
    }

    private void OnVinylInserted(EntityUid uid, VinylPlayerComponent player, ref VinylInsertedEvent args)
    {
        var playerUid = uid;
        var vinylUid = args.Vinyl;

        void QueueSafeEject() => Timer.Spawn(0, () => EjectVinyl(playerUid, vinylUid)); //starlight edit: one-liner, and moved above the Validation

        // Check if the inserted entity has the summon rule component / A song
        if (!TryComp<VinylComponent>(vinylUid, out var vinylComp) //starlight edit: Track any vinyl playing.
            || vinylComp.Song == null)
        {
            QueueSafeEject();
            return;
        }

        // Check if vinyl player is on a station
        if (_stationSystem.GetOwningStation(playerUid) == null)
        {
            _popups.PopupPredicted(Loc.GetString("vinyl-popout-no-station"), playerUid, null, PopupType.Medium);
            QueueSafeEject();
            return;
        }

        // Check if vinyl player is powered
        if (!_power.IsPowered(playerUid))
        {
            _popups.PopupPredicted(Loc.GetString("vinyl-popout-no-power"), playerUid, null, PopupType.Medium);
            QueueSafeEject();
            return;
        }

        // Check if vinyl player is connected to the radio system
        if (!_stationRadio.TryGetLinkedPoweredServer(playerUid, out _)) // Starlight - Station Radio Check oved to StationRadioReceiverSystem
        {
            _popups.PopupPredicted(Loc.GetString("vinyl-popout-no-radio-connection"), playerUid, null, PopupType.Medium);
            QueueSafeEject();
            return;
        }

        // Get the audio length
        var resolved = _audio.ResolveSound(vinylComp.Song);
        var audioLength = _audio.GetAudioLength(resolved);
        var endTime = _timing.CurTime + audioLength;

        // Track this vinyl with its player
        _trackingVinyls[vinylUid] = new TrackingData(playerUid, endTime);
    }

    private void OnVinylRemoved(EntityUid uid, VinylPlayerComponent player, ref VinylRemovedEvent args)
    {
        // Stop tracking if the vinyl is removed
        _trackingVinyls.Remove(args.Vinyl);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _timing.CurTime;

        foreach (var (vinylUid, data) in _trackingVinyls.ToList())
        {
            // Check if the vinyl still exists
            if (!Exists(vinylUid)
                || !Exists(data.VinylPlayerUid))
            {
                _trackingVinyls.Remove(vinylUid);
                continue;
            }

            // Check if vinyl player is still on a station
            if (_stationSystem.GetOwningStation(data.VinylPlayerUid) == null)
            {
                _trackingVinyls.Remove(vinylUid);
                _popups.PopupPredicted(Loc.GetString("vinyl-popout-no-station"), data.VinylPlayerUid, null, PopupType.Medium);
                EjectVinyl(data.VinylPlayerUid, vinylUid);
                continue;
            }

            // Check if vinyl player is still powered
            if (!_power.IsPowered(data.VinylPlayerUid))
            {
                _trackingVinyls.Remove(vinylUid);
                _popups.PopupPredicted(Loc.GetString("vinyl-popout-no-power"), data.VinylPlayerUid, null, PopupType.Medium);
                EjectVinyl(data.VinylPlayerUid, vinylUid);
                continue;
            }

            // Check if vinyl player is still connected to the radio system
            if (!_stationRadio.TryGetLinkedPoweredServer(data.VinylPlayerUid, out _)) // Starlight - Station Radio Check oved to StationRadioReceiverSystem
            {
                _trackingVinyls.Remove(vinylUid);
                _popups.PopupPredicted(Loc.GetString("vinyl-popout-no-radio-connection"), data.VinylPlayerUid, null, PopupType.Medium);
                EjectVinyl(data.VinylPlayerUid, vinylUid);
                continue;
            }

            // Check if playback has finished
            if (currentTime >= data.EndTime)
            {
                #region Starlight lets just... make this a event?
                var ev = new VinylFinishedEvent(data.VinylPlayerUid);
                RaiseLocalEvent(vinylUid, ref ev);
                #endregion
                _trackingVinyls.Remove(vinylUid);
            }
        }
    }

    private void EjectVinyl(EntityUid playerUid, EntityUid vinylUid)
    {
        if (!Exists(vinylUid)
            || !Exists(playerUid)
            || !TryComp<ItemSlotsComponent>(playerUid, out var itemSlots))
            return;

        // Find the slot containing the vinyl
        foreach (var (_, slot) in itemSlots.Slots)
            if (slot.Item == vinylUid)
            {
                _itemSlots.TryEject(playerUid, slot, null, out _);
                return;
            }
    }

    #region Starlight: Eventify Vinyl finishing.
    private void OnVinylFinished(Entity<VinylSummonRuleComponent> entity, ref VinylFinishedEvent _)
    {
        // Resolve the game rule ID and get the threat prototype if available
        var ruleId = ResolveGameRule(entity.Comp.GameRule, out var threat);

        if (ruleId != null)
        {
            _gameTicker.StartGameRule(ruleId, out var _);

            // If we have a threat prototype with an announcement, send it
            if (threat != null)
                _chat.DispatchGlobalAnnouncement(Loc.GetString(threat.Announcement), playSound: true, colorOverride: Color.Red);
        }

        var vinylXform = Transform(entity);
        var vinylCoords = vinylXform.Coordinates;

        // Remove from container
        if (_containers.TryGetContainingContainer((entity, vinylXform, null), out var container))
            _containers.Remove(entity.Owner, container);

        // Play sound effect
        _audio.PlayPvs(entity.Comp.BurnSound, vinylCoords, entity.Comp.BurnSoundParams);  // Starlight - Dehardcode BurnSoundParams

        // Spawn ash at the vinyl's location
        Spawn(entity.Comp.AshPrototype, vinylCoords); // Starlight - Dehardcode ash prototype

        // Delete the vinyl
        QueueDel(entity);
        #endregion
    }

    private string? ResolveGameRule(string gameRuleIdentifier, out NinjaHackingThreatPrototype? threat)
    {
        threat = null;

        // Check if it's a weighted random pool
        if (_prototypeManager.TryIndex<WeightedRandomPrototype>(gameRuleIdentifier, out var weightedPool))
        {
            // Pick a random threat ID from the weighted pool
            var threatId = weightedPool.Pick(_random);

            // Look up the threat prototype to get the actual game rule ID
            if (_prototypeManager.TryIndex<NinjaHackingThreatPrototype>(threatId, out threat))
                return threat.Rule;

            return null;
        }

        // Assume it's a direct game rule entity ID
        return gameRuleIdentifier;
    }
}
