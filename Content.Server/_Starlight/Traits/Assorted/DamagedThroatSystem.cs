using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Traits.Assorted;

public sealed class DamagedThroatSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamagedThroatComponent, EntitySpokeEvent>(OnSpeak);
    }

    private void OnSpeak(EntityUid uid, DamagedThroatComponent component, EntitySpokeEvent args)
    {
        // Don't damage if whispering
        if (args.IsWhisper)
            return;

        // Don't damage if using excluded languages (languages that don't require verbal speech)
        if (args.Language.SpeechOverride.RequireSpeech == false)
            return;

        // Don't damage if on cooldown
        var currentTime = _gameTiming.CurTime;
        if (currentTime < component.LastSpeakTime + component.Cooldown)
            return;

        // Check if reset cooldown has passed (30 seconds of no normal speech)
        if (currentTime >= component.LastSpeakTime + component.ResetCooldown)
        {
            component.CurrentDamage = component.BaseDamage;
        }

        component.LastSpeakTime = currentTime;

        // Apply damage
        var damageType = _prototypeManager.Index<DamageTypePrototype>(component.DamageType);
        var damageSpec = new DamageSpecifier(damageType, component.CurrentDamage);
        _damageableSystem.TryChangeDamage(uid, damageSpec, origin: uid);

        // Chance to cough
        if (_random.Prob(component.CoughChance))
        {
            _chatSystem.TryEmoteWithChat(uid, "Cough");
        }

        // Increase damage for next time (escalating damage)
        component.CurrentDamage = Math.Min(component.CurrentDamage + component.DamageIncrement, component.MaxDamage);
    }
}
