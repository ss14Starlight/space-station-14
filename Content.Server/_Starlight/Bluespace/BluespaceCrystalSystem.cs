using Content.Server._Starlight.Bluespace;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.NullSpace;

public sealed class BluespaceCrystalSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStackSystem _sharedStackSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    public EntProtoId BluespaceCrystalEffect = "EffectFlashBluespace"; //TODO: Change this?
    private const int MaxRandomTeleportAttempts = 20;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceCrystalComponent, LandEvent>((u, c, a) => BluespaceEffect(u, c));
        SubscribeLocalEvent<BluespaceCrystalComponent, ThrowDoHitEvent>(ThrowDoHit);
        SubscribeLocalEvent<BluespaceCrystalComponent, UseInHandEvent>(UseInHand);
    }

    private void UseInHand(EntityUid uid, BluespaceCrystalComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        BluespaceEffect(uid, component, args.User);

        if (HasComp<StackComponent>(uid))
            _sharedStackSystem.ReduceCount(uid, 1);

        args.Handled = true;
    }

    private void ThrowDoHit(EntityUid uid, BluespaceCrystalComponent component, ThrowDoHitEvent args)
    {
        BluespaceEffect(uid, component, args.Target);

        QueueDel(uid);
    }

    public void BluespaceEffect(EntityUid uid, BluespaceCrystalComponent component, EntityUid? target = null)
    {
        var EffectLocation = _transform.GetMapCoordinates(uid);

        Spawn(BluespaceCrystalEffect, EffectLocation);
        
        if (component.Teleport && target is not null && HasComp<MobStateComponent>(target)) // TODO: Do something else? This is there so we dont TELEPORT WALLS/DOORS...
        {
            var newCoords = EffectLocation; // This is a comment... (newCoords is fetch later.)
            for (var i = 0; i < MaxRandomTeleportAttempts; i++)
            {
                newCoords = EffectLocation.Offset(_random.NextVector2(component.Range));
                if (!_lookup.AnyEntitiesIntersecting(newCoords, LookupFlags.Static))
                    break;
            }
            _transform.SetCoordinates(target.Value, _transform.ToCoordinates(newCoords));
        }

        if (component.NullSpaceShunt)
            DoNullSpaceShuntEffect(_transform.ToCoordinates(EffectLocation), component.NullSpaceShuntRange);
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