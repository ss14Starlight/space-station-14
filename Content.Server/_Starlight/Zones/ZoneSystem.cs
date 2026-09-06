using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Zones;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem : SharedZoneSystem
{
    private int _maxTilesPerTick = 32;

    private int _maxRenamesPerTick = 2;

    private int _maxSearchVisits = 6144;

    private int _maxSeeds = 5;

    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(StarlightCCVars.MaxZoneTilesPerTick, x => _maxTilesPerTick = x, true);
        _cfg.OnValueChanged(StarlightCCVars.MaxZoneRenamesPerTick, x => _maxRenamesPerTick = x, true);
        _cfg.OnValueChanged(StarlightCCVars.MaxZoneSearchVisits, x => _maxSearchVisits = x, true);
        _cfg.OnValueChanged(
            StarlightCCVars.MaxZoneSeeds,
            x =>
            {
                _maxSeeds = Math.Max(5, x);
                _seedBuffer = new List<Vector2i>(_maxSeeds);
                _anchorRegions = new ushort[_maxSeeds];
                _anchors = new Vector2i[_maxSeeds];
            },
            true);
    }
}
