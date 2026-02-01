using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Holiday;
using Content.Shared.CCVar;
using Content.Shared.Starlight.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Maps;

public sealed class GameMapManager : IGameMapManager
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceManager _resMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly HolidaySystem _holiday = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    private readonly Queue<string> _previousMaps = new();
    [ViewVariables(VVAccess.ReadOnly)]
    private List<GameMapPrototype?>? _configSelectedMaps;
    [ViewVariables(VVAccess.ReadOnly)]
    private List<GameMapPrototype?>? _selectedMaps; // Don't change this value during a round!
    [ViewVariables(VVAccess.ReadOnly)]
    private bool _mapRotationEnabled;
    [ViewVariables(VVAccess.ReadOnly)]
    private int _mapQueueDepth = 1;

    private ISawmill _log = default!;

    public void Initialize()
    {
        _log = Logger.GetSawmill("mapsel");

        _configurationManager.OnValueChanged(CCVars.GameMap, value =>
        {
            if (TryLookupMap(value, out GameMapPrototype? map))
            {
                if (map != null)
                {
                    _configSelectedMaps = new List<GameMapPrototype?> { map };
                }
                else
                {
                    _configSelectedMaps = null;
                }
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                _configSelectedMaps = default!;
                return;
            }

            if (_configurationManager.GetCVar<bool>(CCVars.UsePersistence))
            {
                var startMap = _configurationManager.GetCVar<string>(CCVars.PersistenceMap);
                _configSelectedMaps = new List<GameMapPrototype?> { _prototypeManager.Index<GameMapPrototype>(startMap) };

                var mapPath = new ResPath(value);
                if (_resMan.UserData.Exists(mapPath) && _configSelectedMaps != null && _configSelectedMaps[0] != null)
                {
                    _configSelectedMaps[0] = _configSelectedMaps[0]?.Persistence(mapPath);
                    _log.Info($"Using persistence map from {value}");
                    return;
                }

                // persistence save path doesn't exist so we just use the start map
                _log.Warning($"Using persistence start map {startMap} as {value} doesn't exist");
                return;
            }

            _log.Error($"Unknown map prototype {value} was selected!");
        }, true);
        _configurationManager.OnValueChanged(CCVars.GameMapRotation, value => _mapRotationEnabled = value, true);
        _configurationManager.OnValueChanged(CCVars.GameMapMemoryDepth, value =>
        {
            _mapQueueDepth = value;
            // Drain excess.
            while (_previousMaps.Count > _mapQueueDepth)
            {
                _previousMaps.Dequeue();
            }
        }, true);

        var maps = AllVotableMaps().ToArray();
        _random.Shuffle(maps);
        foreach (var map in maps)
        {
            if (_previousMaps.Count >= _mapQueueDepth)
                break;
            _previousMaps.Enqueue(map.ID);
        }
    }

    public IEnumerable<GameMapPrototype> CurrentlyEligibleMaps()
    {
        var maps = AllVotableMaps().Where(IsMapEligible).ToArray();
        return maps.Length == 0 ? AllMaps().Where(x => x.Fallback) : maps;
    }

    public IEnumerable<GameMapPrototype> AllVotableMaps()
    {
        var poolPrototype = _entityManager.System<GameTicker>().Preset?.MapPool ??
                   _configurationManager.GetCVar(CCVars.GameMapPool);
        
        /*
        if (_holiday.IsCurrentlyHoliday("NewYear"))
        {
            Logger.Debug("[Rinary] Map pool changed to christmas! Have a good Christmas!");
            poolPrototype = "ChristmasStarlightMapPool";
        }
        */

        if (_prototypeManager.TryIndex<GameMapPoolPrototype>(poolPrototype, out var pool))
        {
            foreach (var map in pool.Maps)
            {
                if (!_prototypeManager.TryIndex<GameMapPrototype>(map, out var mapProto))
                {
                    _log.Error($"Couldn't index map {map} in pool {poolPrototype}");
                    continue;
                }

                yield return mapProto;
            }
        }
        else
        {
            throw new Exception($"Could not index map pool prototype {poolPrototype}!");
        }
    }

    public IEnumerable<GameMapPrototype> AllMaps()
    {
        return _prototypeManager.EnumeratePrototypes<GameMapPrototype>();
    }

    public List<GameMapPrototype?> GetSelectedMaps()
    {
        return _configSelectedMaps ?? _selectedMaps ?? new List<GameMapPrototype?>();
    }

    public void ClearSelectedMaps()
    {
        _selectedMaps = default!;
    }

    public bool TrySelectMapIfEligible(string gameMap)
    {
        if (!TryLookupMap(gameMap, out var map) || !IsMapEligible(map))
            return false;
        
        _selectedMaps ??= new List<GameMapPrototype?>();

        _selectedMaps.Add(map);
        return true;
    }

    public string GetMapString()
    {
        if (_selectedMaps == null || _selectedMaps.Count == 0)
            return "No map selected";

        return string.Join(", ", _selectedMaps.Select(map => map?.MapName ?? Loc.GetString("discord-round-notifications-unknown-map")));
    }

    public int GetStationCount()
    {
        return _configurationManager.GetCVar(StarlightCCVars.StationCount);
    }

    public bool TrySelectMapsIfEligible(List<string> gameMaps)
    {
        _selectedMaps = new List<GameMapPrototype?>();
        foreach (var gameMap in gameMaps)
        {
            if (!TryLookupMap(gameMap, out var map) || !IsMapEligible(map))
                return false;
            _selectedMaps.Add(map);
        }
        return true;
    }

    public void SelectMaps(List<string> gameMaps)
    {
        _selectedMaps = new List<GameMapPrototype?>();
        foreach (var gameMap in gameMaps)
        {
            if (!TryLookupMap(gameMap, out var map))
                throw new ArgumentException($"The map \"{gameMap}\" is invalid!");
            _selectedMaps.Add(map);
        }
    }

    public void SelectMapsRandom()
    {
        var maps = CurrentlyEligibleMaps().ToList();
        _selectedMaps = new List<GameMapPrototype?>();
        for (var i = 0; i < GetStationCount(); i++)
        {
            if (maps.Count == 0)
                break;
            _selectedMaps.Add(_random.Pick(maps));
        }
    }

    public void SelectMapsFromRotationQueue(bool markAsPlayed = false)
    {
        _selectedMaps = new List<GameMapPrototype?>();
        for (var i = 0; i < GetStationCount(); i++)
        {
            if (_previousMaps.Count == 0)
                break;

            var map = GetFirstInRotationQueue();
            _selectedMaps.Add(map);
            if (markAsPlayed)
                EnqueueMap(map.ID);
        }
    }

    public void SelectMapsByConfigRules()
    {
        if (_mapRotationEnabled)
        {
            _log.Info("selecting the next maps from the rotation queue");
            SelectMapsFromRotationQueue(true);
        }
        else
        {
            _log.Info("selecting random maps");
            SelectMapsRandom();
        }
    }

    public bool CheckMapExists(string gameMap)
    {
        return TryLookupMap(gameMap, out _);
    }

    private bool IsMapEligible(GameMapPrototype map)
    {
        var modifiedPlayerCount = _playerManager.PlayerCount / GetStationCount(); //make sure its minimum 1
        modifiedPlayerCount = Math.Max(modifiedPlayerCount, 1);
        return map.MaxPlayers >= modifiedPlayerCount &&
               map.MinPlayers <= modifiedPlayerCount &&
               map.Conditions.All(x => x.Check(map)) &&
               _entityManager.System<GameTicker>().IsMapEligible(map);
    }

    private bool TryLookupMap(string gameMap, [NotNullWhen(true)] out GameMapPrototype? map)
    {
        return _prototypeManager.TryIndex(gameMap, out map);
    }

    private int GetMapRotationQueuePriority(string gameMapProtoName)
    {
        var i = 0;
        foreach (var map in _previousMaps.Reverse())
        {
            if (map == gameMapProtoName)
                return i;
            i++;
        }
        return _mapQueueDepth;
    }

    private GameMapPrototype GetFirstInRotationQueue()
    {
        _log.Info($"map queue: {string.Join(", ", _previousMaps)}");

        var eligible = CurrentlyEligibleMaps()
            .Select(x => (proto: x, weight: GetMapRotationQueuePriority(x.ID)))
            .OrderByDescending(x => x.weight)
            .ToArray();

        _log.Info($"eligible queue: {string.Join(", ", eligible.Select(x => (x.proto.ID, x.weight)))}");

        // YML "should" be configured with at least one fallback map
        Debug.Assert(eligible.Length != 0, $"couldn't select a map with {nameof(GetFirstInRotationQueue)}()! No eligible maps and no fallback maps!");

        var weight = eligible[0].weight;
        return eligible.Where(x => x.Item2 == weight)
            .MinBy(x => x.proto.ID)
            .proto;
    }

    private void EnqueueMap(string mapProtoName)
    {
        _previousMaps.Enqueue(mapProtoName);
        while (_previousMaps.Count > _mapQueueDepth)
        {
            _previousMaps.Dequeue();
        }
    }
}
