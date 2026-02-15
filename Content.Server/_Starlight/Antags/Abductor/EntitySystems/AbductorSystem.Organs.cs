using Content.Shared.Starlight.Antags.Abductor;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Server.Chat.Systems;
using Content.Shared.Damage.Systems;

namespace Content.Server.Starlight.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private float _delayAccumulator = 0f;
    private readonly Stopwatch _stopwatch = new();
    private readonly DamageSpecifier _passiveHealing = new();
    private readonly Dictionary<AbductorOrganType, TimeSpan> _organCooldowns = new()
    {
        { AbductorOrganType.Health, TimeSpan.FromSeconds(3) },
        { AbductorOrganType.NitrousOxide, TimeSpan.FromSeconds(120) },
        { AbductorOrganType.Gravity, TimeSpan.FromSeconds(60) },
        { AbductorOrganType.Egg, TimeSpan.FromSeconds(120) },
        { AbductorOrganType.Spider, TimeSpan.FromSeconds(240) },
    };

    public void InitializeOrgans()
    {
        foreach (var specif in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
            _passiveHealing.DamageDict.Add(specif.ID, -3);

        _stopwatch.Start();
    }

    public override void Update(float frameTime)
    {
        _delayAccumulator += frameTime;

        if (_delayAccumulator < 3f)
            return;

        _delayAccumulator = 0f;
        _stopwatch.Restart();

        var query = EntityQueryEnumerator<AbductorVictimComponent>();
        while (query.MoveNext(out var uid, out var victim) && _stopwatch.Elapsed < TimeSpan.FromMilliseconds(0.5))
        {
            if (victim.Organ == AbductorOrganType.None)
                continue;

            TryActivateOrgan(uid, victim);
        }
    }

    private void TryActivateOrgan(EntityUid uid, AbductorVictimComponent victim)
    {
        if (!_organCooldowns.TryGetValue(victim.Organ, out var cooldown))
            return;

        if (_time.CurTime - victim.LastActivation < cooldown)
            return;

        victim.LastActivation = _time.CurTime;

        switch (victim.Organ)
        {
            case AbductorOrganType.Health:
                _damageable.TryChangeDamage(uid, _passiveHealing);
                break;
            case AbductorOrganType.NitrousOxide:
                HandleNitrousOxideOrgan(uid);
                break;

            case AbductorOrganType.Gravity:
                HandleGravityOrgan(uid);
                break;

            case AbductorOrganType.Egg:
                SpawnAttachedTo("FoodEggChickenFertilized", Transform(uid).Coordinates);
                break;

            case AbductorOrganType.Spider:
                SpawnAttachedTo("MobSpiderlingSpiderAngry", Transform(uid).Coordinates);
                break;
        }
    }

    private void HandleNitrousOxideOrgan(EntityUid uid)
    {
        var mix = _atmos.GetContainingMixture((uid, Transform(uid)), true, true) ?? new();
        mix.AdjustMoles(Gas.NitrousOxide, 30);
        _chat.TryEmoteWithChat(uid, "Cough");
    }

    private void HandleGravityOrgan(EntityUid uid)
    {
        var gravity = SpawnAttachedTo("AbductorGravityGlandGravityWell", Transform(uid).Coordinates);
        _xformSys.SetParent(gravity, uid);
    }
}
