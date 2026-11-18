using System.Numerics;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Short screen effects for hits and blasts.
/// </summary>
public sealed partial class ScreenImpactSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// Seconds for the colour split to fade to about a third.
    /// </summary>
    private const float AberrationFade = 0.35f;

    /// <summary>
    /// Seconds for the flash to fade to about a third.
    /// </summary>
    private const float FlashFade = 0.15f;

    /// <summary>
    /// Damage in one hit that gives the full colour split.
    /// </summary>
    private const float FullDamage = 30f;

    /// <summary>
    /// Seconds after taking a new body in which damage is ignored, so the first damage state is not read as a hit.
    /// </summary>
    private static readonly TimeSpan _attachGrace = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How far the colours split, from 0 to 1, already scaled by the player's strength setting.
    /// </summary>
    public float Aberration { get; private set; }

    /// <summary>
    /// How strong the flash is, from 0 to 1, already scaled by the player's strength setting.
    /// </summary>
    public float Flash { get; private set; }

    /// <summary>
    /// Colour of the current flash in linear light.
    /// </summary>
    public Vector3 FlashColor { get; private set; } = Vector3.One;

    private bool _enabled;
    private float _strength;
    private EntityUid? _localEntity;
    private TimeSpan _ignoreDamageUntil;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.ImpactEffects, v => _enabled = v, true);
        Subs.CVar(_cfg, StarlightCCVars.ImpactEffectsStrength, v => _strength = Math.Clamp(v, 0f, 1f), true);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity != _localEntity)
        {
            _localEntity = _player.LocalEntity;
            _ignoreDamageUntil = _timing.RealTime + _attachGrace;
        }

        Aberration *= MathF.Exp(-frameTime / AberrationFade);
        Flash *= MathF.Exp(-frameTime / FlashFade);

        if (Aberration < 0.001f)
            Aberration = 0f;
        if (Flash < 0.001f)
            Flash = 0f;
    }

    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (!_enabled
            || ent.Owner != _player.LocalEntity
            || !args.DamageIncreased
            || args.DamageDelta == null
            || !_timing.IsFirstTimePredicted
            || _timing.RealTime < _ignoreDamageUntil)
        {
            return;
        }

        var damage = args.DamageDelta.GetTotal().Float();
        if (damage <= 0f)
            return;

        Kick(Math.Clamp(damage / FullDamage, 0.15f, 1f), 0f, Vector3.One);
    }

    [SubscribeLocalEvent]
    private void OnExplosionStartup(Entity<ExplosionVisualsComponent> ent, ref ComponentStartup args)
    {
        if (!_enabled)
            return;

        var eye = _eye.CurrentEye;
        var epicenter = ent.Comp.Epicenter;
        if (eye.Position.MapId != epicenter.MapId)
            return;

        // The explosion reaches about as many tiles as it has intensity steps. The effect fades out at three times that.
        var radius = MathF.Max(ent.Comp.Intensity.Count, 1f);
        var distance = (eye.Position.Position - epicenter.Position).Length();
        var reach = radius * 3f;
        if (distance >= reach)
            return;

        var amount = Math.Clamp(radius / 8f, 0.25f, 1f) * (1f - (distance / reach));
        var color = Color.White;
        if (_proto.TryIndex<ExplosionPrototype>(ent.Comp.ExplosionType, out var type))
            color = Color.FromSrgb(type.LightColor);

        Kick(amount, amount * 0.6f, new Vector3(color.R, color.G, color.B));
    }

    private void Kick(float aberration, float flash, Vector3 flashColor)
    {
        Aberration = MathF.Max(Aberration, aberration * _strength);

        var scaledFlash = flash * _strength;
        if (scaledFlash > Flash)
        {
            Flash = scaledFlash;
            FlashColor = flashColor;
        }
    }
}
