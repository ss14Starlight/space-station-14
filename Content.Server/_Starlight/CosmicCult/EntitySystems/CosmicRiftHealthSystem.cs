using Content.Server.Destructible;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared.GameTicking;

namespace Content.Server._Starlight.CosmicCult.EntitySystems;

public sealed partial class CosmicRiftHealthSystem : EntitySystem
{
    [Dependency] private CosmicMalignEmpoweredRiftSystem _riftSystem = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    /// <summary>
    /// Tracks escalating global warnings as the number of stored corpses increases.
    /// Higher corpse counts indicate that the Colossus threat is progressing.
    /// </summary>

    private bool _corpseWarning1;
    private bool _corpseWarning2;
    private bool _corpseWarning3;
    private float? _corpseWarning3ScreamTimer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _corpseWarning1 = false;
        _corpseWarning2 = false;
        _corpseWarning3 = false;
        _corpseWarning3ScreamTimer = null;
    }

    /// <summary>
    /// Updates the cosmic rift health state and triggers global warnings as the
    /// number of stored corpses reaches the configured threat thresholds.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var corpseCount = _riftSystem.StoredCorpseCount;

        if (_corpseWarning3ScreamTimer.HasValue)
        {
            _corpseWarning3ScreamTimer -= frameTime;

            if (_corpseWarning3ScreamTimer <= 0f)
            {
                _corpseWarning3ScreamTimer = null;

                _audio.PlayGlobal(
                    new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/colossus_scream.ogg"),
                    Filter.Broadcast(),
                    true,
                    AudioParams.Default.WithVolume(5f));
            }
        }

        if (!_corpseWarning1 && corpseCount >= 5)
        {
            _corpseWarning1 = true;

            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("cosmiccult-rift-corpse1-warning"),
                playSound: false,
                colorOverride: Color.FromHex("#cae8e8"));

            _audio.PlayGlobal(
                new SoundPathSpecifier("/Audio/_Starlight/Misc/notice1.ogg"),
                Filter.Broadcast(),
                true);
        }

        if (!_corpseWarning2 && corpseCount >= 10)
        {
            _corpseWarning2 = true;

            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("cosmiccult-rift-corpse2-warning"),
                playSound: false,
                colorOverride: Color.Red);

            _audio.PlayGlobal(
                new SoundPathSpecifier("/Audio/Misc/redalert.ogg"),
                Filter.Broadcast(),
                true);
        }

        if (!_corpseWarning3 && corpseCount >= 20)
        {
            _corpseWarning3 = true;
            _corpseWarning3ScreamTimer = 5f;

            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("cosmiccult-rift-corpse3-warning"),
                playSound: false,
                colorOverride: Color.Red);

            _audio.PlayGlobal(
                new SoundPathSpecifier("/Audio/Misc/redalert.ogg"),
                Filter.Broadcast(),
                true);
        }

        // if 10 has been reached, but count gone back to 2 we can reset the alarms.
        if (_corpseWarning2 && corpseCount <= 2)
        {
            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("cosmiccult-rift-corpse-dewarning"),
                playSound: false,
                colorOverride: Color.Green);

            _audio.PlayGlobal(
                new SoundPathSpecifier("/Audio/_Starlight/Misc/notice1.ogg"),
                Filter.Broadcast(),
                true);

            _corpseWarning1 = false;
            _corpseWarning2 = false;
            _corpseWarning3 = false;
            _corpseWarning3ScreamTimer = null;
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
