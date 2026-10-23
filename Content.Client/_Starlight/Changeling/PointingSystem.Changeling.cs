using Content.Client.Pointing.Components;
using Content.Shared._Starlight.Changeling;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Client.Pointing;

public sealed partial class PointingSystem
{
    [Dependency] private SharedMapSystem _hivePointMap = default!;

    private void InitializeChangelingPointing()
    {
        SubscribeNetworkEvent<ChangelingPointingEvent>(OnChangelingPointing);
    }

    private void OnChangelingPointing(ChangelingPointingEvent args)
    {
        if (!_hivePointMap.MapExists(args.Target.MapId) || args.Target.MapId != args.Origin.MapId)
            return;

        var overrides = new ComponentRegistry
        {
            ["PointingArrow"] = new(new PointingArrowComponent
            {
                StartPosition = args.Origin.Position - args.Target.Position,
            }),
            ["TimedDespawn"] = new(new TimedDespawnComponent
            {
                Lifetime = (float) PointDuration.TotalSeconds,
            }),
        };
        Spawn("PointingArrow", args.Target, overrides);
    }
}
