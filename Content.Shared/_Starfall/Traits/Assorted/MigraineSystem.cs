using Content.Shared._Starlight.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starfall.Traits.Assorted;

/// <summary>
/// Handles effects when a migraine status effect is applied to an entity.
/// </summary>
public sealed partial class MigraineSystem : EntitySystem
{
    private static readonly EntProtoId SparkEffect = "EffectSparks";
    private static readonly SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks");
    private static readonly TimeSpan NeocyteSparkInterval = TimeSpan.FromSeconds(4);

    private static readonly SoundSpecifier MigraineSound = new SoundPathSpecifier("/Audio/_Starfall/Effects/migraine.ogg");

    [Dependency] private MovementSpeedModifierSystem _movementSpeed = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private SharedJitteringSystem _jittering = null!;
    [Dependency] private Content.Shared.StatusEffect.StatusEffectsSystem _statusEffects = null!;
    [Dependency] private StatusEffectsSystem _newStatusEffects = null!;
    [Dependency] private INetManager _net = null!;
    [Dependency] private IGameTiming _timing = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MigraineEffectComponent, StatusEffectAppliedEvent>(OnMigraineApplied);
        SubscribeLocalEvent<MigraineEffectComponent, StatusEffectRemovedEvent>(OnMigraineRemoved);
        SubscribeLocalEvent<Content.Shared.StatusEffect.StatusEffectsComponent, Content.Shared.StatusEffect.StatusEffectEndedEvent>(OnTimedStatusEnded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<MigraineEffectComponent, StatusEffectComponent>();

        while (query.MoveNext(out _, out var migraine, out var status))
        {
            if (status.AppliedTo is not { } target || _timing.CurTime < migraine.NextNeocyteSparkTime || !IsNeocyte(target))
                continue;

            PlayNeocyteSparks(target);
            migraine.NextNeocyteSparkTime = _timing.CurTime + NeocyteSparkInterval;
        }
    }

    private void OnMigraineApplied(Entity<MigraineEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(args.Target);

        if (_net.IsServer)
            _jittering.AddJitter(args.Target, -85f, 2f);

        if (_net.IsServer)
            _audio.PlayEntity(MigraineSound, args.Target, args.Target);

        var isNeocyte = IsNeocyte(args.Target);

        if (_net.IsServer && isNeocyte)
        {
            PlayNeocyteSparks(args.Target);
            entity.Comp.NextNeocyteSparkTime = _timing.CurTime + NeocyteSparkInterval;
        }

        if (!_net.IsServer)
            return;

        if (!entity.Comp.ShowPopup)
            return;

        var popupEvent = new MigrainePopupEvent(isNeocyte
                ? "trait-chronic-migraines-start-neocyte"
                : entity.Comp.SelfPopup,
            isNeocyte
                ? "trait-chronic-migraines-others-neocyte"
                : entity.Comp.OthersPopup);

        // Other systems can modify or cancel the popup.
        RaiseLocalEvent(args.Target, popupEvent);

        if (popupEvent.Cancelled)
            return;

        if (popupEvent.SelfPopup != null)
            _popup.PopupEntity(Loc.GetString(popupEvent.SelfPopup), args.Target, args.Target, PopupType.MediumCaution);

        if (popupEvent.OthersPopup != null)
            _popup.PopupEntity(Loc.GetString(popupEvent.OthersPopup, ("target", args.Target)), args.Target, Filter.PvsExcept(args.Target), true, PopupType.SmallCaution);
    }

    private void OnMigraineRemoved(Entity<MigraineEffectComponent> entity, ref StatusEffectRemovedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(args.Target);

        if (!_net.IsServer)
            return;

        if (_statusEffects.HasStatusEffect(args.Target, "Jitter"))
            return;

        RemCompDeferred<JitteringComponent>(args.Target);
    }

    private void OnTimedStatusEnded(EntityUid uid, Content.Shared.StatusEffect.StatusEffectsComponent component,
        Content.Shared.StatusEffect.StatusEffectEndedEvent args)
    {
        if (!_net.IsServer || args.Key != "Jitter")
            return;

        if (_newStatusEffects.HasStatusEffect(uid, MigraineEffectComponent.Prototype))
            _jittering.AddJitter(uid, -85f, 2f);
        else
            RemCompDeferred<JitteringComponent>(uid);
    }

    private bool IsNeocyte(EntityUid target)
    {
        return HasComp<NeocyteComponent>(target);
    }

    private void PlayNeocyteSparks(EntityUid target)
    {
        Spawn(SparkEffect, Transform(target).Coordinates);
        _audio.PlayPvs(SparkSound, target, SparkSound.Params.WithVariation(0.05f));
    }
}
