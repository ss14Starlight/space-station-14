using Content.Shared._Starlight.Bluespace;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Bluespace;

public sealed partial class BluespaceCrystalSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStackSystem _sharedStackSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    public EntProtoId BluespaceCrystalEffect = "EffectFlashBluespace";
    public EntProtoId BluespaceCrystalEffectSimple = "EffectFlashBluespaceSimple";
    private const int MaxRandomTeleportAttempts = 20;
    public override void Initialize() => base.Initialize();

    [SubscribeLocalEvent]
    private void UseInHand(EntityUid uid, BluespaceCrystalComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        BluespaceEffect(uid, component, args.User);

        if (HasComp<StackComponent>(uid))
            _sharedStackSystem.ReduceCount(uid, 1);

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void ThrowDoHit(EntityUid uid, BluespaceCrystalComponent component, ThrowDoHitEvent args)
    {
        BluespaceEffect(uid, component, args.Target);

        QueueDel(uid);
    }

    [SubscribeLocalEvent]
    private void LandEvent(EntityUid uid, BluespaceCrystalComponent component, LandEvent args)
    {
        BluespaceEffect(uid, component, effect: BluespaceCrystalEffectSimple);

        QueueDel(uid);
    }

    public void BluespaceEffect(
        EntityUid uid,
        BluespaceCrystalComponent component,
        EntityUid? target = null,
        EntProtoId? effect = null)
    {
        if (component.Used)
            return;
        else
            component.Used = true;

        var EffectLocation = _transform.ToCoordinates(_transform.GetMapCoordinates(uid));

        SpawnAtPosition(effect ?? BluespaceCrystalEffect, EffectLocation);

        if (component.NullSpaceShunt)
            DoNullSpaceShuntEffect(EffectLocation, component.NullSpaceShuntRange);

        if (component.Teleport && target is not null && HasComp<MobStateComponent>(target))
        {
            var newCoords = EffectLocation.Offset(_random.NextVector2(component.Range));
            for (var i = 0; i < MaxRandomTeleportAttempts; i++)
            {
                var randVector = _random.NextVector2(component.Range);
                newCoords = EffectLocation.Offset(randVector);
                if (!_lookup.AnyEntitiesIntersecting(_transform.ToMapCoordinates(newCoords), LookupFlags.Static))
                    break;
            }

            _transform.SetCoordinates(target.Value, newCoords);
        }
    }

    private void DoNullSpaceShuntEffect(EntityCoordinates coordinates, float range)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(coordinates, range))
        {
            var ev = new NullSpaceShuntEvent();
            RaiseLocalEvent(uid, ref ev);
        }
    }
}
