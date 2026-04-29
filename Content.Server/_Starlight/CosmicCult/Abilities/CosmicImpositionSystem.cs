using Content.Server.Popups;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.NullSpace;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed class CosmicImpositionSystem : EntitySystem
{
    [Dependency] private readonly CosmicCultSystem _cult = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicImposingComponent, BeforeDamageChangedEvent>(OnImpositionDamaged);
        SubscribeLocalEvent<CosmicCultComponent, EventCosmicImposition>(OnCosmicImposition);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CosmicImposingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.Expiry)
            {
                RemComp(uid, comp);
            }
        }
    }

    private void OnCosmicImposition(Entity<CosmicCultComponent> uid, ref EventCosmicImposition args)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, uid);
                return;
            }

        EnsureComp<CosmicImposingComponent>(uid, out var comp);
        comp.Expiry = _timing.CurTime + uid.Comp.CosmicImpositionDuration;
        Spawn(uid.Comp.ImpositionVFX, Transform(uid).Coordinates);
        args.Handled = true;
        _audio.PlayPvs(uid.Comp.ImpositionSFX, uid, AudioParams.Default.WithVariation(0.05f));
        _cult.MalignEcho(uid);
    }

    private void OnImpositionDamaged(Entity<CosmicImposingComponent> uid, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }
}
