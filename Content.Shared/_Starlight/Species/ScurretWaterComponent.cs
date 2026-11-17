using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Species;

[RegisterComponent]
public sealed partial class ScurretWaterComponent : Component
{
    public TimeSpan NextWaterHealTime;
}

public sealed partial class ScurretWaterSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly DamageSpecifier WaterHealing = new()
    {
        DamageDict =
        {
            ["Poison"] = -0.2f,
            ["Cold"] = -0.4f,
            ["Blunt"] = -0.2f,
        },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScurretWaterComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ScurretWaterComponent, IngestedEvent>(OnIngested);
        SubscribeLocalEvent<ScurretWaterComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
    }

    private void OnIngested(Entity<ScurretWaterComponent> ent, ref IngestedEvent args)
    {
        var salt = args.Split.GetTotalPrototypeQuantity("TableSalt");
        if (salt <= 0)
            return;

        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Poison"] = FixedPoint2.New(salt.Float() * 10f),
            },
        };

        _damageable.TryChangeDamage(ent.Owner, damage, origin: args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ScurretWaterComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextWaterHealTime)
                continue;

            component.NextWaterHealTime = _timing.CurTime + TimeSpan.FromSeconds(1);
            foreach (var puddleUid in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, 0.5f))
            {
                if (TryComp<WaterTileComponent>(puddleUid, out _))
                {
                    _damageable.TryChangeDamage(uid, WaterHealing, true, origin: puddleUid);
                    break;
                }

                if (!TryComp<PuddleComponent>(puddleUid, out var puddle) ||
                    !_solutions.ResolveSolution(puddleUid, puddle.SolutionName, ref puddle.Solution, out var solution) ||
                    solution.Volume <= 0 ||
                    solution.GetTotalPrototypeQuantity("Water").Float() / solution.Volume.Float() < 0.51f)
                {
                    continue;
                }

                _damageable.TryChangeDamage(uid, WaterHealing, true, origin: puddleUid);
                break;
            }
        }
    }

    private void OnDamageModify(Entity<ScurretWaterComponent> ent, ref DamageModifyEvent args)
    {
        if (args.CanHeal ||
            !TryComp<ThirstComponent>(ent, out var thirst) ||
            !IsThirstDamageState(thirst.CurrentThirstThreshold) ||
            !args.Damage.AnyPositive())
        {
            return;
        }

        args.Damage *= _random.NextFloat(1.05f, 1.10f);
    }

    private void OnRefreshMovementSpeed(Entity<ScurretWaterComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<ThirstComponent>(ent, out var thirst) ||
            !IsThirstDamageState(thirst.CurrentThirstThreshold))
        {
            return;
        }

        args.ModifySpeed(0.8f, 0.8f);
    }

    private static bool IsThirstDamageState(ThirstThreshold threshold)
    {
        return threshold is ThirstThreshold.Thirsty or ThirstThreshold.Parched or ThirstThreshold.Dead;
    }
}
