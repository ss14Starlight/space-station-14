using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Sound;
using Content.Shared.Atmos;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Sound;

public sealed partial class HearingPressureSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
        => EnsureComp<HearingPressureComponent>(args.Entity);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<HearingPressureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hearing, out var xform))
        {
            var pressure = HasComp<GhostComponent>(uid)
                ? Atmospherics.OneAtmosphere
                : _atmosphere.GetContainingMixture((uid, xform))?.Pressure ?? 0f;

            pressure = MathF.Min(MathF.Round(pressure / HearingPressureComponent.Step) * HearingPressureComponent.Step,
                Atmospherics.OneAtmosphere);

            var sealedHelmet = _inventory.TryGetSlotEntity(uid, "head", out var helmet)
                && HasComp<PressureProtectionComponent>(helmet);

            if (hearing.Pressure.Equals(pressure) && hearing.SealedHelmet == sealedHelmet)
                continue;

            hearing.Pressure = pressure;
            hearing.SealedHelmet = sealedHelmet;
            Dirty(uid, hearing);
        }
    }
}
