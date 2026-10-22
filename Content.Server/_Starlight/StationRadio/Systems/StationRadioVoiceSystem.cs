using Content.Server._Starlight.TextToSpeech;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Radio.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class StationRadioVoiceSystem : EntitySystem
{
    private const float LowVolumeModifier = 0.6f;

    private const float InterferenceVolumeLoss = 0.5f;

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationRadioReceiverSystem _stationRadio = default!;
    [Dependency] private TTSSystem _tts = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
    }

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        if (args.SuppressTTS)
            return;

        var groups = GetListenerGroups(args.Channel.ID, args.Receivers);
        if (groups.Count == 0)
            return;

        _tts.PlayAtSpeakers(args.Source, args.Message.Tts ?? args.Message.Text, args.Language, args.Channel.ID, groups);
    }

    /// <summary>
    /// Station radios that read out the channel, each with the players who hear it from that radio
    /// (the closest one in range) and how loud its voice is.
    /// </summary>
    public List<(EntityUid Speaker, Filter Listeners, float Volume)> GetListenerGroups(string channel, IEnumerable<EntityUid> excluded)
    {
        var groups = new List<(EntityUid, Filter, float)>();

        // Every radio that would read this message out, with how far it carries and how loud the voice is.
        var speakers = new List<(EntityUid Uid, MapCoordinates Coords, float Range, float Volume)>();
        var query = EntityQueryEnumerator<StationRadioReceiverComponent, ActiveRadioComponent, RadioSpeakerComponent>();
        while (query.MoveNext(out var uid, out var receiver, out var radio, out var speaker))
        {
            if (!receiver.Active
                || !speaker.Enabled
                || !radio.Channels.Contains(channel)
                || !_power.IsPowered(uid)
                || !_stationRadio.TryGetPoweredGridServer(uid, out var server)
                || !TryComp<StationRadioServerComponent>(server, out var serverComp)
                || serverComp.VoiceGain <= 0f)
                continue;

            var param = receiver.BoostVolume ? receiver.BoostedParams : receiver.DefaultParams;
            var volume = serverComp.VoiceGain
                * (receiver.BoostVolume ? 1f : LowVolumeModifier)
                * (1f - InterferenceVolumeLoss * receiver.Interference);
            speakers.Add((uid, _transform.GetMapCoordinates(uid), param.MaxDistance, volume));
        }

        if (speakers.Count == 0)
            return groups;

        var headsets = new HashSet<EntityUid>(excluded);
        var listeners = new Dictionary<int, List<ICommonSession>>();
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } listener || headsets.Contains(listener))
                continue;

            var coords = _transform.GetMapCoordinates(listener);
            var nearest = -1;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < speakers.Count; i++)
            {
                var speaker = speakers[i];
                if (speaker.Coords.MapId != coords.MapId)
                    continue;

                var distance = (speaker.Coords.Position - coords.Position).Length();
                if (distance > speaker.Range || distance >= nearestDistance)
                    continue;

                nearest = i;
                nearestDistance = distance;
            }

            if (nearest < 0)
                continue;

            if (!listeners.TryGetValue(nearest, out var group))
                listeners[nearest] = group = new List<ICommonSession>();
            group.Add(session);
        }

        foreach (var (index, sessions) in listeners)
        {
            var speaker = speakers[index];
            groups.Add((speaker.Uid, Filter.Empty().AddPlayers(sessions), speaker.Volume));
        }

        return groups;
    }
}
