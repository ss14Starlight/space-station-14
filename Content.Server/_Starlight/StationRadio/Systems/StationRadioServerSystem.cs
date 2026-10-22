using System.Linq;
using Content.Server._Starlight.TextToSpeech;
using Content.Server.Radio;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class StationRadioServerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedCargoSystem _cargo = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationRadioReceiverSystem _stationRadio = default!;
    [Dependency] private VinylPlayerSystem _vinylPlayer = default!;

    private const float InterferenceStep = 0.05f;

    private const float GarbleChance = 0.35f;

    private const string RadioShowChannel = "RadioShow";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StationRadioServerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Refresh((uid, comp));

            if (now >= comp.NextAdvertSample)
            {
                comp.NextAdvertSample = now + comp.AdvertSampleInterval;
                SampleAdverts((uid, comp));
            }

            if (now < comp.NextListenerCount)
                continue;

            comp.NextListenerCount = now + comp.ListenerCountInterval;
            var listeners = CountListeners((uid, comp));
            if (listeners == comp.Listeners)
                continue;

            comp.Listeners = listeners;
            var ev = new StationRadioTracksChangedEvent();
            RaiseLocalEvent(uid, ref ev);
        }
    }

    /// <summary>
    /// Works out whether music is muted and how disturbed the broadcast is, and tells the radios if that changed.
    /// </summary>
    public void Refresh(Entity<StationRadioServerComponent> ent)
    {
        var comp = ent.Comp;
        var now = _timing.CurTime;

        var musicMuted = comp.MusicCut || now < comp.MusicMutedUntil;

        var interference = 0f;
        if (comp.InterferenceCut)
            interference = comp.WireInterference;
        if (now < comp.InterferenceUntil)
            interference = MathF.Max(interference, comp.BurstInterference);
        if (TryComp<DamageableComponent>(ent, out var damageable))
        {
            var damage = _damageable.GetTotalDamage((ent, damageable)).Float();
            var fraction = (damage - comp.DamageInterferenceStart) / (comp.DamageInterferenceFull - comp.DamageInterferenceStart);
            interference = MathF.Max(interference, Math.Clamp(fraction, 0f, 1f) * comp.DamageInterference);
        }

        interference = MathF.Round(interference / InterferenceStep) * InterferenceStep;

        if (musicMuted == comp.MusicMuted && MathHelper.CloseTo(interference, comp.Interference))
            return;

        comp.MusicMuted = musicMuted;
        comp.Interference = interference;
        _stationRadio.SyncReceivers(ent);
    }

    [SubscribeLocalEvent]
    private void OnEmpPulse(Entity<StationRadioServerComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        var until = _timing.CurTime + args.Duration;
        if (until > ent.Comp.InterferenceUntil)
            ent.Comp.InterferenceUntil = until;

        Refresh(ent);
    }

    [SubscribeLocalEvent]
    private void OnRadioSendAttempt(Entity<StationRadioServerComponent> ent, ref RadioSendAttemptEvent args)
    {
        // A voice fader at zero mutes the host completely, chat included.
        if (IsMicrophoneMuted(ent.Comp) || ent.Comp.VoiceGain <= 0f)
            args.Cancelled = true;
    }

    public bool IsMicrophoneMuted(StationRadioServerComponent comp) =>
        comp.MicrophoneCut || _timing.CurTime < comp.MicrophoneMutedUntil;

    /// <summary>
    /// Breaks up a message heard through a disturbed radio: some letters are lost to static.
    /// </summary>
    public static string Garble(string message, float interference, IRobustRandom random)
    {
        if (interference <= 0f)
            return message;

        var chance = GarbleChance * interference;
        var chars = message.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]) && random.Prob(chance))
                chars[i] = '~';
        }

        return new string(chars);
    }

    #region Listeners

    /// <summary>
    /// Players within earshot of a working station radio of this server.
    /// </summary>
    public int CountListeners(Entity<StationRadioServerComponent> server)
    {
        var grid = Transform(server).GridUid;
        if (grid == null || !_power.IsPowered(server.Owner))
            return 0;

        var radios = new List<(MapCoordinates Coords, float Range)>();
        var query = EntityQueryEnumerator<StationRadioReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var receiver, out var xform))
        {
            if (xform.GridUid != grid || !receiver.Active || !_power.IsPowered(uid))
                continue;

            var param = receiver.BoostVolume ? receiver.BoostedParams : receiver.DefaultParams;
            radios.Add((_transform.GetMapCoordinates(uid, xform), param.MaxDistance));
        }

        if (radios.Count == 0)
            return 0;

        var count = 0;
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } listener
                || HasComp<GhostComponent>(listener)
                || _mobState.IsDead(listener))
                continue;

            var coords = _transform.GetMapCoordinates(listener);
            foreach (var (radio, range) in radios)
            {
                if (radio.MapId != coords.MapId || (radio.Position - coords.Position).Length() > range)
                    continue;

                count++;
                break;
            }
        }

        return count;
    }

    #endregion

    #region Adverts

    private const float SpeechCharsPerSecond = 14f;

    private static readonly TimeSpan MinSpeech = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan MaxSpeech = TimeSpan.FromSeconds(15);

    private const float MinAdvertQuality = 0.05f;

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        if (args.Channel.ID != RadioShowChannel)
            return;

        var length = TimeSpan.FromSeconds(args.Message.Text.Length / SpeechCharsPerSecond);
        length = length < MinSpeech ? MinSpeech : length > MaxSpeech ? MaxSpeech : length;
        var until = _timing.CurTime + length;

        var query = EntityQueryEnumerator<StationRadioServerComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.VoiceGain > 0f && until > comp.VoiceOnAirUntil)
                comp.VoiceOnAirUntil = until;
        }
    }

    private void SampleAdverts(Entity<StationRadioServerComponent> server)
    {
        var comp = server.Comp;
        var now = _timing.CurTime;

        foreach (var disc in comp.AdvertAirtime.Keys.ToArray())
        {
            if (TerminatingOrDeleted(disc))
                comp.AdvertAirtime.Remove(disc);
        }

        var music = comp.MusicMuted ? 0f : comp.MasterGain;
        var voice = now < comp.VoiceOnAirUntil ? comp.VoiceGain : 0f;
        foreach (var (source, _) in comp.Tracks)
        {
            if (AdvertOf(source) is not { } advert)
                continue;

            var advertGain = music * _stationRadio.GetTrackGain(comp, source);
            var overlap = voice;
            foreach (var (other, _) in comp.Tracks)
            {
                if (other != source)
                    overlap += music * _stationRadio.GetTrackGain(comp, other);
            }

            var quality = advertGain * (1f - Math.Clamp(overlap, 0f, 1f));
            var (score, samples) = comp.AdvertAirtime.GetValueOrDefault(advert);
            comp.AdvertAirtime[advert] = (score + quality, samples + 1);
        }
    }

    private EntityUid? AdvertOf(EntityUid source)
    {
        if (!TryComp<VinylPlayerComponent>(source, out var player) || !player.Playing)
            return null;

        var disc = _vinylPlayer.GetCurrentDisc((source, player));
        return HasComp<RadioAdvertComponent>(disc) ? disc : null;
    }

    [SubscribeLocalEvent]
    private void OnAdvertFinished(Entity<RadioAdvertComponent> ent, ref VinylFinishedEvent args)
    {
        if (!TryComp<VinylPlayerComponent>(args.Player, out var player)
            || player.Standalone
            || !_stationRadio.TryGetLinkedPoweredServer(args.Player, out var serverUid)
            || !TryComp<StationRadioServerComponent>(serverUid, out var server)
            || server.MusicMuted)
            return;

        var now = _timing.CurTime;
        var (score, samples) = server.AdvertAirtime.GetValueOrDefault(ent);
        server.AdvertAirtime.Remove(ent);

        if (now - server.LastAdvertPaid < server.AdGlobalCooldown)
        {
            var left = server.AdGlobalCooldown - (now - server.LastAdvertPaid);
            _popup.PopupEntity(Loc.GetString("radio-advert-popup-too-soon", ("minutes", (int) Math.Ceiling(left.TotalMinutes))),
                args.Player,
                PopupType.Small);
            return;
        }

        var advert = MetaData(ent).EntityPrototype?.ID ?? Name(ent);
        if (server.AdsPaid.TryGetValue(advert, out var paidAt) && now - paidAt < server.AdCooldown)
        {
            _popup.PopupEntity(Loc.GetString("radio-advert-popup-repeat"), args.Player, PopupType.Small);
            return;
        }

        var quality = samples > 0 ? score / samples : 1f;
        if (quality < MinAdvertQuality)
        {
            _popup.PopupEntity(Loc.GetString("radio-advert-popup-drowned"), args.Player, PopupType.Small);
            return;
        }

        server.Listeners = CountListeners((serverUid, server));
        if (server.Listeners == 0)
        {
            _popup.PopupEntity(Loc.GetString("radio-advert-popup-no-listeners"), args.Player, PopupType.Small);
            return;
        }

        if (_station.GetOwningStation(serverUid) is not { } station
            || !TryComp<StationBankAccountComponent>(station, out var bank))
            return;

        var accounts = bank.Accounts;
        if (!accounts.ContainsKey(ent.Comp.Account))
            return;

        var full = Math.Min(server.Listeners * ent.Comp.RewardPerListener, ent.Comp.MaxReward);
        var reward = (int) MathF.Round(full * quality);
        _cargo.UpdateBankAccount((station, bank), reward, ent.Comp.Account);
        server.AdsPaid[advert] = now;
        server.LastAdvertPaid = now;

        _popup.PopupEntity(Loc.GetString("radio-advert-popup-paid",
                ("reward", reward),
                ("listeners", server.Listeners),
                ("quality", (int) MathF.Round(quality * 100))),
            args.Player,
            PopupType.Medium);
    }

    #endregion
}
