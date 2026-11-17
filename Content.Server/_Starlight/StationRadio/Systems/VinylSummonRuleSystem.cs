using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Communications;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Chat.Systems;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class VinylSummonRuleSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private ChatSystem _chat = default!;

    [SubscribeLocalEvent]
    private void OnVinylFinished(Entity<VinylSummonRuleComponent> entity, ref VinylFinishedEvent args)
    {
        if (!TryComp<VinylPlayerComponent>(args.Player, out var player)
            || player.Standalone
            || _stationSystem.GetOwningStation(args.Player) == null)
            return;

        var ruleId = ResolveGameRule(entity.Comp.GameRule, out var threat);

        if (ruleId != null)
        {
            _gameTicker.StartGameRule(ruleId, out var _);

            if (threat != null)
                _chat.DispatchGlobalAnnouncement(Loc.GetString(threat.Announcement), playSound: true, colorOverride: Color.Red);
        }

        var vinylXform = Transform(entity);
        var vinylCoords = vinylXform.Coordinates;

        if (_containers.TryGetContainingContainer((entity, vinylXform, null), out var container))
            _containers.Remove(entity.Owner, container);

        _audio.PlayPvs(entity.Comp.BurnSound, vinylCoords, entity.Comp.BurnSoundParams);

        Spawn(entity.Comp.AshPrototype, vinylCoords);

        QueueDel(entity);
    }

    private string? ResolveGameRule(string gameRuleIdentifier, out NinjaHackingThreatPrototype? threat)
    {
        threat = null;

        if (_prototypeManager.TryIndex<WeightedRandomPrototype>(gameRuleIdentifier, out var weightedPool))
        {
            var threatId = weightedPool.Pick(_random);

            if (_prototypeManager.TryIndex<NinjaHackingThreatPrototype>(threatId, out threat))
                return threat.Rule;

            return null;
        }

        return gameRuleIdentifier;
    }
}
