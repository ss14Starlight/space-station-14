using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.StatusEffect;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Robust.Server.Player;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Daiquiri.Temperature;

public sealed partial class ShiverWhenColdSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;

    private const float ColdScale = 0.33f;
    private static readonly TimeSpan ShiverInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShiverDuration = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private static readonly string[] ShiverMessages =
    {
        "shiver-cold-message-1",
        "shiver-cold-message-2",
        "shiver-cold-message-3",
        "shiver-cold-message-4",
        "shiver-cold-message-5",
        "shiver-cold-message-6",
        "shiver-cold-message-7",
        "shiver-cold-message-8",
        "shiver-cold-message-9",
        "shiver-cold-message-10",
        "shiver-cold-message-11",
        "shiver-cold-message-12",
    };

    private readonly Dictionary<EntityUid, TimeSpan> _nextShiver = new();

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemperatureDamageComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var query = EntityQueryEnumerator<TemperatureDamageComponent, TemperatureComponent, HumanoidAppearanceComponent, StatusEffectsComponent>();
        while (query.MoveNext(out var uid, out var damage, out var temperature, out _, out var status))
        {
            if (!IsCold(uid, damage, temperature))
            {
                _nextShiver.Remove(uid);
                continue;
            }

            if (_nextShiver.TryGetValue(uid, out var next) && _timing.CurTime < next)
                continue;

            _nextShiver[uid] = _timing.CurTime + ShiverInterval;

            var scale = GetColdScale(uid, damage, temperature);
            var severity = Math.Clamp((scale - ColdScale) / (1f - ColdScale), 0f, 1f);
            var amplitude = 1.5f + severity * 2.5f;

            _jittering.DoJitter(uid, ShiverDuration, refresh: true, amplitude: amplitude, frequency: 3f, status: status);
            SendShiverMessage(uid);
        }
    }

    private bool IsCold(EntityUid uid, TemperatureDamageComponent damage, TemperatureComponent temperature)
        => GetColdScale(uid, damage, temperature) >= ColdScale;

    private float GetColdScale(EntityUid uid, TemperatureDamageComponent damage, TemperatureComponent temperature)
    {
        var coldThreshold = damage.ParentColdDamageThreshold ?? damage.ColdDamageThreshold;
        var heatThreshold = damage.ParentHeatDamageThreshold ?? damage.HeatDamageThreshold;
        var ideal = (coldThreshold + heatThreshold) / 2f;

        if (TryComp<ThermalRegulatorComponent>(uid, out var regulator) &&
            regulator.NormalBodyTemperature > coldThreshold &&
            regulator.NormalBodyTemperature < heatThreshold)
        {
            ideal = regulator.NormalBodyTemperature;
        }

        if (ideal <= coldThreshold)
            return 0f;

        return (temperature.CurrentTemperature - ideal) / (coldThreshold - ideal);
    }

    private void SendShiverMessage(EntityUid uid)
    {
        if (!_player.TryGetSessionByEntity(uid, out var session))
            return;

        var message = Loc.GetString(_random.Pick(ShiverMessages));
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", FormattedMessage.EscapeText(message)));
        _chat.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, uid, false, session.Channel, Color.LightSkyBlue);
    }

    private void OnTerminating(Entity<TemperatureDamageComponent> ent, ref EntityTerminatingEvent args)
    {
        _nextShiver.Remove(ent);
    }
}
