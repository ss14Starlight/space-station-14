using Content.Server.Anomaly.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Materials;
using Content.Server.Radiation.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Anomaly.Prototypes;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
#region Starlight
using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Content.Shared.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Content.Shared._Starlight.CosmicCult.Components;
#endregion

namespace Content.Server.Anomaly;

/// <summary>
/// This handles logic and interactions relating to <see cref="AnomalyComponent"/>
/// </summary>
public sealed partial class AnomalySystem : SharedAnomalySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AmbientSoundSystem _ambient = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MaterialStorageSystem _material = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RadiationSystem _radiation = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    #region Starlight
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EmitterSystem _emitter = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    #endregion

    public const float MinParticleVariation = 0.8f;
    public const float MaxParticleVariation = 1.2f;

    private static readonly ProtoId<WeightedRandomPrototype> WeightListProto = "AnomalyBehaviorList";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnomalyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnomalyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AnomalyComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<AnomalyStabilityChangedEvent>(OnVesselAnomalyStabilityChanged);
        #region Starlight
        SubscribeLocalEvent<AnomalyComponent, AnomalyParticleInteractionDoAfterEvent>(OnParticleInteractionDoAfter);
        #endregion

        InitializeGenerator();
        InitializeVessel();
        InitializeCommands();
    }

    private void OnMapInit(Entity<AnomalyComponent> anomaly, ref MapInitEvent args)
    {
        // Starlight edit Start
        if (anomaly.Comp.CanPulse)
            anomaly.Comp.NextPulseTime = Timing.CurTime + GetPulseLength(anomaly.Comp) * 3; // longer the first time
        // Starlight edit End
        ChangeAnomalyStability(anomaly, Random.NextFloat(anomaly.Comp.InitialStabilityRange.Item1 , anomaly.Comp.InitialStabilityRange.Item2), anomaly.Comp);
        ChangeAnomalySeverity(anomaly, Random.NextFloat(anomaly.Comp.InitialSeverityRange.Item1, anomaly.Comp.InitialSeverityRange.Item2), anomaly.Comp);

        // Starlight edit Start
        if (anomaly.Comp.ShuffleParticlesOnMapInit)
            ShuffleParticlesEffect(anomaly);

        anomaly.Comp.Continuity = _random.NextFloat(anomaly.Comp.MinContituty, anomaly.Comp.MaxContituty);

        if (anomaly.Comp.RandomBehaviorOnMapInit)
            SetBehavior(anomaly, GetRandomBehavior());
        // Starlight edit End
    }

    public void ShuffleParticlesEffect(Entity<AnomalyComponent> anomaly)
    {
        var particles = new List<AnomalousParticleType>
            { AnomalousParticleType.Delta, AnomalousParticleType.Epsilon, AnomalousParticleType.Zeta, AnomalousParticleType.Sigma };

        anomaly.Comp.SeverityParticleType = Random.PickAndTake(particles);
        anomaly.Comp.DestabilizingParticleType = Random.PickAndTake(particles);
        anomaly.Comp.WeakeningParticleType = Random.PickAndTake(particles);
        anomaly.Comp.TransformationParticleType = Random.PickAndTake(particles);
        Dirty(anomaly);
    }

    private void OnShutdown(Entity<AnomalyComponent> anomaly, ref ComponentShutdown args)
    {
        if (anomaly.Comp.CurrentBehavior is not null)
            RemoveBehavior(anomaly, anomaly.Comp.CurrentBehavior.Value);

        // Starlight Start
        if (anomaly.Comp.Ending)
            return;
        // Starlight End

        EndAnomaly(anomaly, anomaly.Comp, spawnCore: false, removeComponent: false); // Starlight Edit: Added anomaly.comp and removeComponent
    }

    private void OnStartCollide(Entity<AnomalyComponent> anomaly, ref StartCollideEvent args)
    {
        if (!TryComp<AnomalousParticleComponent>(args.OtherEntity, out var particle))
            return;

        if (args.OtherFixtureId != particle.FixtureId)
            return;

        var behaviorMod = 1f;
        if (anomaly.Comp.CurrentBehavior != null)
        {
            var b = _prototype.Index(anomaly.Comp.CurrentBehavior.Value);
            behaviorMod = b.ParticleSensivity;
        }
        // small function to randomize because it's easier to read like this
        float VaryValue(float v) => v * behaviorMod * Random.NextFloat(MinParticleVariation, MaxParticleVariation);

        #region Starlight Edit
        if (anomaly.Comp.UseNormalUnstableParticle &&
            (particle.ParticleType == anomaly.Comp.DestabilizingParticleType || particle.DestabilzingOverride))
        {
            ChangeAnomalyStability(anomaly, VaryValue(particle.StabilityPerDestabilizingHit), anomaly.Comp);
        }

        if (anomaly.Comp.UseNormalDangerParticle &&
            (particle.ParticleType == anomaly.Comp.SeverityParticleType || particle.SeverityOverride))
        {
            ChangeAnomalySeverity(anomaly, VaryValue(particle.SeverityPerSeverityHit), anomaly.Comp);
        }

        if (anomaly.Comp.UseNormalContainmentParticle &&
            (particle.ParticleType == anomaly.Comp.WeakeningParticleType || particle.WeakeningOverride))
        {
            ChangeAnomalyHealth(anomaly, VaryValue(particle.HealthPerWeakeningeHit), anomaly.Comp);
            ChangeAnomalyStability(anomaly, VaryValue(particle.StabilityPerWeakeningeHit), anomaly.Comp);
        }

        if (anomaly.Comp.UseNormalTransformationParticle &&
            (particle.ParticleType == anomaly.Comp.TransformationParticleType || particle.TransmutationOverride))
        {
            ChangeAnomalySeverity(anomaly, VaryValue(particle.SeverityPerSeverityHit), anomaly.Comp);

            if (_random.Prob(anomaly.Comp.Continuity))
                SetBehavior(anomaly, GetRandomBehavior());
        }

        TryStartParticleInteraction(anomaly, particle, args.OtherEntity);
        #endregion

        var ev = new AnomalyAffectedByParticleEvent(anomaly, args.OtherEntity);
        RaiseLocalEvent(anomaly, ref ev);
    }

    /// <summary>
    /// Gets the amount of research points generated per second for an anomaly.
    /// </summary>
    /// <param name="anomaly"></param>
    /// <param name="component"></param>
    /// <returns>The amount of points</returns>
    public int GetAnomalyPointValue(EntityUid anomaly, AnomalyComponent? component = null)
    {
        if (!Resolve(anomaly, ref component, false))
            return 0;

        var multiplier = 1f;
        if (component.Stability > component.GrowthThreshold)
            multiplier = component.GrowingPointMultiplier; //more points for unstable

        //penalty of up to 50% based on health
        multiplier *= MathF.Pow(1.5f, component.Health) - 0.5f;

        //Apply behavior modifier
        if (component.CurrentBehavior != null)
        {
            var behavior = _prototype.Index(component.CurrentBehavior.Value);
            multiplier *= behavior.EarnPointModifier;
        }

        var severityValue = 1 / (1 + MathF.Pow(MathF.E, -7 * (component.Severity - 0.5f)));

        return (int) ((component.MaxPointsPerSecond - component.MinPointsPerSecond) * severityValue * multiplier) + component.MinPointsPerSecond;
    }

    /// <summary>
    /// Gets the localized name of a particle.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public string GetParticleLocale(AnomalousParticleType type)
    {
        return type switch
        {
            AnomalousParticleType.Delta => Loc.GetString("anomaly-particles-delta"),
            AnomalousParticleType.Epsilon => Loc.GetString("anomaly-particles-epsilon"),
            AnomalousParticleType.Zeta => Loc.GetString("anomaly-particles-zeta"),
            AnomalousParticleType.Sigma => Loc.GetString("anomaly-particles-sigma"),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateGenerator();
        UpdateVessels();
    }

    #region Behavior
    private string GetRandomBehavior()
    {
        var weightList = _prototype.Index(WeightListProto);
        return weightList.Pick(_random);
    }

    private void SetBehavior(Entity<AnomalyComponent> anomaly, ProtoId<AnomalyBehaviorPrototype> behaviorProto)
    {
        if (anomaly.Comp.CurrentBehavior == behaviorProto)
            return;

        if (anomaly.Comp.CurrentBehavior != null)
            RemoveBehavior(anomaly, anomaly.Comp.CurrentBehavior.Value);

        anomaly.Comp.CurrentBehavior = behaviorProto;
        var behavior = _prototype.Index(behaviorProto);
        EntityManager.AddComponents(anomaly, behavior.Components);

        var ev = new AnomalyBehaviorChangedEvent(anomaly, anomaly.Comp.CurrentBehavior, behaviorProto);
        RaiseLocalEvent(anomaly, ref ev, true);
    }

    private void RemoveBehavior(Entity<AnomalyComponent> anomaly, ProtoId<AnomalyBehaviorPrototype> behaviorProto)
    {
        if (anomaly.Comp.CurrentBehavior == null)
            return;

        var behavior = _prototype.Index(behaviorProto);

        EntityManager.RemoveComponents(anomaly, behavior.Components);
    }

    #region Starlight
    private void TryStartParticleInteraction(
        Entity<AnomalyComponent> anomaly,
        AnomalousParticleComponent particle,
        EntityUid particleUid)
    {
        if (anomaly.Comp.ParticleInteractions.Count == 0)
            return;

        if (_doAfter.IsRunning(anomaly.Comp.ParticleInteractionDoAfter))
            return;

        var interactionIndex = GetMatchingParticleInteraction(anomaly.Comp, particle);

        if (interactionIndex == null)
            return;

        var interaction = anomaly.Comp.ParticleInteractions[interactionIndex.Value];

        if (!TryComp<ProjectileComponent>(particleUid, out var projectile) ||
            projectile.Shooter is not { } shooter)
            return;

        var source = GetPoweredNullspaceStabilizerSource(shooter);
        if (source == null)
            return;

        if (interaction.Delay <= TimeSpan.Zero)
        {
            ApplyParticleInteraction(anomaly, interaction, shooter);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            shooter,
            interaction.Delay,
            new AnomalyParticleInteractionDoAfterEvent(),
            anomaly,
            anomaly)
        {
            NeedHand = false,
            BreakOnWeightlessMove = true,
            BreakOnMove = true,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            BreakOnDamage = false,
            RequireCanInteract = false,
            DistanceThreshold = interaction.DistanceThreshold,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return;

        anomaly.Comp.ParticleInteractionDoAfter = doAfterId;
        anomaly.Comp.ActiveParticleInteraction = interactionIndex;
        anomaly.Comp.ActiveParticleInteractionSource = shooter;

        source.DoAfterId = doAfterId;
    }

    private static int? GetMatchingParticleInteraction(
        AnomalyComponent anomaly,
        AnomalousParticleComponent particle)
    {
        for (var i = 0; i < anomaly.ParticleInteractions.Count; i++)
        {
            var interaction = anomaly.ParticleInteractions[i];

            if (interaction.InteractionKey != null &&
                interaction.InteractionKey == particle.InteractionKey)
                return i;

            if (interaction.ParticleType != null &&
                interaction.ParticleType == particle.ParticleType)
                return i;
        }

        return null;
    }

    private void OnParticleInteractionDoAfter(
        Entity<AnomalyComponent> anomaly,
        ref AnomalyParticleInteractionDoAfterEvent args)
    {
        var source = anomaly.Comp.ActiveParticleInteractionSource;

        anomaly.Comp.ParticleInteractionDoAfter = null;
        anomaly.Comp.ActiveParticleInteractionSource = null;

        if (source is { } sourceUid &&
            TryComp<CosmicLambdaParticleSourceComponent>(sourceUid, out var sourceComp))
        {
            sourceComp.DoAfterId = null;
        }

        if (args.Cancelled || args.Handled)
        {
            anomaly.Comp.ActiveParticleInteraction = null;
            return;
        }

        args.Handled = true;

        if (anomaly.Comp.ActiveParticleInteraction is not { } index ||
            index < 0 ||
            index >= anomaly.Comp.ParticleInteractions.Count)
        {
            anomaly.Comp.ActiveParticleInteraction = null;
            return;
        }

        var interaction = anomaly.Comp.ParticleInteractions[index];

        anomaly.Comp.ActiveParticleInteraction = null;

        ApplyParticleInteraction(anomaly, interaction, args.User);
    }

    private void ApplyParticleInteraction(
        Entity<AnomalyComponent> anomaly,
        AnomalyParticleInteraction interaction,
        EntityUid user)
    {
        var coords = Transform(anomaly).Coordinates;

        if (TryComp<EmitterComponent>(user, out var emitter))
            _emitter.PowerOff(user, emitter);

        if (interaction.VisualEffect is { } effectProto)
            Spawn(effectProto, coords);

        if (interaction.Sound is { } sound)
            _audio.PlayPvs(sound, coords);

        switch (interaction.Effect)
        {
            case AnomalyParticleInteractionEffect.EndAnomaly:
                EndAnomaly(anomaly, anomaly.Comp, spawnCore: interaction.SpawnCore);

                if (interaction.DeleteEntityAfterEnd && !TerminatingOrDeleted(anomaly.Owner))
                    QueueDel(anomaly.Owner);

                break;

            case AnomalyParticleInteractionEffect.DeleteEntity:
                QueueDel(anomaly);
                break;
        }
    }

    private CosmicLambdaParticleSourceComponent? GetPoweredNullspaceStabilizerSource(EntityUid uid)
        => !TryComp<CosmicLambdaParticleSourceComponent>(uid, out var source)
        ? null : !_powerReceiver.IsPowered(uid)
        ? null : source;

    #endregion

    #endregion

    #region Information
    /// <summary>
    /// Get a formatted message with a summary of all anomaly information for putting on a UI.
    /// </summary>
    public FormattedMessage GetScannerMessage(AnomalyScannerComponent component)
    {
        var msg = new FormattedMessage();
        if (component.ScannedAnomaly is not { } anomaly || !TryComp<AnomalyComponent>(anomaly, out var anomalyComp))
        {
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-no-anomaly"));
            return msg;
        }

        TryComp<SecretDataAnomalyComponent>(anomaly, out var secret);

        //Severity
        if (secret != null && secret.Secret.Contains(AnomalySecretData.Severity) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-severity-percentage-unknown"));
        else
        {
            var text = Loc.GetString("anomaly-scanner-severity-percentage", ("percent", anomalyComp.Severity.ToString("P")));
            if (secret != null && secret.Secret.Contains(AnomalySecretData.Severity))
                text += " " + Loc.GetString("anomaly-secret-admin");
            msg.AddMarkupOrThrow(text);
        }
        msg.PushNewline();

        //Stability
        if (secret != null && secret.Secret.Contains(AnomalySecretData.Stability) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-stability-unknown"));
        else
        {
            string stateLoc;
            if (anomalyComp.Stability < anomalyComp.DecayThreshold)
                stateLoc = Loc.GetString("anomaly-scanner-stability-low");
            else if (anomalyComp.Stability > anomalyComp.GrowthThreshold)
                stateLoc = Loc.GetString("anomaly-scanner-stability-high");
            else
                stateLoc = Loc.GetString("anomaly-scanner-stability-medium");

            if (secret != null && secret.Secret.Contains(AnomalySecretData.Stability))
                stateLoc += " " + Loc.GetString("anomaly-secret-admin");

            msg.AddMarkupOrThrow(stateLoc);
        }
        msg.PushNewline();

        //Point output
        if (secret != null && secret.Secret.Contains(AnomalySecretData.OutputPoint) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-point-output-unknown"));
        else
        {
            var text = Loc.GetString("anomaly-scanner-point-output", ("point", GetAnomalyPointValue(anomaly, anomalyComp)));
            if (secret != null && secret.Secret.Contains(AnomalySecretData.OutputPoint))
                text += " " + Loc.GetString("anomaly-secret-admin");
            msg.AddMarkupOrThrow(text);
        }
        msg.PushNewline();
        msg.PushNewline();

        //Particles title
        msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-particle-readout"));
        msg.PushNewline();

        //Danger
        if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleDanger) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-particle-danger-unknown"));
        else
        {
            var text = Loc.GetString("anomaly-scanner-particle-danger", ("type", GetParticleLocale(anomalyComp.SeverityParticleType)));
            if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleDanger))
                text += " " + Loc.GetString("anomaly-secret-admin");
            msg.AddMarkupOrThrow(text);
        }
        msg.PushNewline();

        //Unstable
        if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleUnstable) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-particle-unstable-unknown"));
        else
        {
            var text = Loc.GetString("anomaly-scanner-particle-unstable", ("type", GetParticleLocale(anomalyComp.DestabilizingParticleType)));
            if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleUnstable))
                text += " " + Loc.GetString("anomaly-secret-admin");
            msg.AddMarkupOrThrow(text);
        }
        msg.PushNewline();

        //Containment
        if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleContainment) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-particle-containment-unknown"));
        else
        {
            // Starlight edit Start
            var particleName = anomalyComp.ScannerContainmentParticleReadout is { } containmentReadout
                ? Loc.GetString(containmentReadout)
                : GetParticleLocale(anomalyComp.WeakeningParticleType);

            var text = Loc.GetString("anomaly-scanner-particle-containment", ("type", particleName));
            // Starlight edit End
            if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleContainment))
                text += " " + Loc.GetString("anomaly-secret-admin");
            msg.AddMarkupOrThrow(text);
        }
        msg.PushNewline();

        //Transformation
        if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleTransformation) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-scanner-particle-transformation-unknown"));
        else
        {
            var text = Loc.GetString("anomaly-scanner-particle-transformation", ("type", GetParticleLocale(anomalyComp.TransformationParticleType)));
            if (secret != null && secret.Secret.Contains(AnomalySecretData.ParticleTransformation))
                text += " " + Loc.GetString("anomaly-secret-admin");
            msg.AddMarkupOrThrow(text);
        }


        //Behavior
        msg.PushNewline();
        msg.PushNewline();
        var behaviorTitle = Loc.GetString("anomaly-behavior-title");
        if (secret != null && secret.Secret.Contains(AnomalySecretData.Behavior) && component.IgnoreSecret)
            behaviorTitle += " " + Loc.GetString("anomaly-secret-admin");
        msg.AddMarkupOrThrow(behaviorTitle);
        msg.PushNewline();

        if (secret != null && secret.Secret.Contains(AnomalySecretData.Behavior) && !component.IgnoreSecret)
            msg.AddMarkupOrThrow(Loc.GetString("anomaly-behavior-unknown"));
        else
        {
            if (anomalyComp.CurrentBehavior != null)
            {
                var behavior = _prototype.Index(anomalyComp.CurrentBehavior.Value);

                msg.AddMarkupOrThrow("- " + Loc.GetString(behavior.Description));
                msg.PushNewline();
                var mod = Math.Floor((behavior.EarnPointModifier) * 100);
                msg.AddMarkupOrThrow("- " + Loc.GetString("anomaly-behavior-point", ("mod", mod)));
            }
            else
            {
                msg.AddMarkupOrThrow(Loc.GetString("anomaly-behavior-balanced"));
            }
        }

        //The timer at the end here is actually added in the ui itself.
        return msg;
    }
    #endregion
}
