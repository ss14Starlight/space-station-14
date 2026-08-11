using Content.Server.Destructible;
using Content.Server._Starlight.CosmicCult.Components;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared._Starlight.CosmicCult.Components;

namespace Content.Server._Starlight.CosmicCult;

public sealed class CosmicRiftHealthSystem : EntitySystem
{
    [Dependency] private readonly CosmicMalignEmpoweredRiftSystem _riftSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private bool _corpseWarning10;
    private bool _corpseWarning15;
    private bool _corpseWarning20;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var corpseCount = _riftSystem.StoredCorpseCount;
    
        if (!_corpseWarning10 && corpseCount >= 10)
        {
            _corpseWarning10 = true;

            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("cosmiccult-rift-corpse10-warning"),
                playSound: false,
                colorOverride: Color.FromHex("#cae8e8"));

            _audio.PlayGlobal(
                new SoundPathSpecifier("/Audio/_Starlight/Misc/notice1.ogg"),
                Filter.Broadcast(),
                true);
        }

        if (!_corpseWarning15 && corpseCount >= 15)
        {
            _corpseWarning15 = true;

            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("cosmiccult-rift-corpse15-warning"),
                playSound: false,
                colorOverride: Color.Red);

            _audio.PlayGlobal(
                new SoundPathSpecifier("/Audio/Misc/redalert.ogg"),
                Filter.Broadcast(),
                true);
        }

        if (!_corpseWarning20 && corpseCount >= 20)
        {
            _corpseWarning20 = true;

            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("cosmiccult-rift-corpse20-warning"),
                playSound: false,
                colorOverride: Color.Red);

            var colossusQuery = EntityQueryEnumerator<CosmicColossusComponent>();

            while (colossusQuery.MoveNext(out var colossusUid, out var colossus))
            {
             _audio.PlayGlobal(
                colossus.ScreamSfx,
                Filter.Broadcast(),
                true,
                AudioParams.Default.WithVolume(35f));

            _audio.PlayGlobal(
                new SoundPathSpecifier("/Audio/Misc/redalert.ogg"),
                Filter.Broadcast(),
                true);
            }
        }

        var query = EntityQueryEnumerator<
            CosmicRiftHealthComponent,
            MobThresholdsComponent,
            DamageableComponent,
            DestructibleComponent>();

        while (query.MoveNext(
                   out var uid,
                   out var health,
                   out var thresholds,
                   out var damageable,
                   out var destructible))
        {
            var desiredBonus = corpseCount * health.HealthPerCorpse;
            var bonusDifference = desiredBonus - health.AppliedCorpseBonus;

            if (bonusDifference == 0)
                continue;

            // Increase the normal mob death threshold.
            if (_mobThreshold.TryGetDeadThreshold(uid, out var currentMaxHealth, thresholds))
            {
                var newMaxHealth = currentMaxHealth.Value + bonusDifference;

                _mobThreshold.SetMobStateThreshold(
                    uid,
                    newMaxHealth,
                    MobState.Dead,
                    thresholds);
            }

            // Increase the destruction threshold by the same amount.
            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is not DamageTrigger damageTrigger)
                    continue;

                damageTrigger.Damage += bonusDifference;
            }

            // Remember how much bonus health has already been applied.
            health.AppliedCorpseBonus = desiredBonus;

            // Re-check the mob state after changing its health threshold.
            _mobThreshold.VerifyThresholds(uid, thresholds, damageable: damageable);
        }
    }
}
