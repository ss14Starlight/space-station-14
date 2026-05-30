using Content.Server.Popups;
using Content.Shared._FarHorizons.Silicons.Glitching;
using Content.Shared._FarHorizons.VFX;
using Content.Shared.Emp;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._FarHorizons.Silicons.Glitching;

public sealed class GlitchingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private TimeSpan _refreshRate = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GlitchOnEMPComponent, EmpPulseEvent>((ent, ref _) =>
            ApplyGlitch(ent.Owner, ent.Comp.Duration, ent.Comp.Ramp));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + _refreshRate;

        var query = EntityQueryEnumerator<GlitchingEffectComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.FinishAt)
                RemCompDeferred<GlitchingEffectComponent>(uid);
        }
    }

    public void ApplyGlitch(EntityUid uid, TimeSpan effectDuration, TimeSpan effectRamp)
    {
        var comp = EnsureComp<GlitchingEffectComponent>(uid);
        comp.Animated = true;
        comp.StartAt = _timing.CurTime;
        comp.FinishAt = _timing.CurTime + effectDuration;
        comp.RampDuration = effectRamp;
        Dirty<GlitchingEffectComponent>((uid, comp));
    }

    public void TriggerIonStorm(Entity<GlitchOnIonStormComponent> ent)
    {
        ApplyGlitch(ent.Owner, ent.Comp.Duration, ent.Comp.Ramp);
        _popup.PopupEntity(Loc.GetString("glitch-on-ion-storm-start-message"), ent.Owner, ent.Owner, PopupType.LargeCaution);
    }
}
