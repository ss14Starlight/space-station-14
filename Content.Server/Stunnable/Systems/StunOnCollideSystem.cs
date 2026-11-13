using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Server.Stunnable.Components;
using Content.Shared.Movement.Systems;
using JetBrains.Annotations;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Events;
using Content.Shared.Projectiles;

namespace Content.Server.Stunnable.Systems;

[UsedImplicitly]
internal sealed partial class StunOnCollideSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!; // Starlight- adding admin logging
    [Dependency] private StunSystem _stunSystem = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunOnCollideComponent, StartCollideEvent>(HandleCollide);
        SubscribeLocalEvent<StunOnCollideComponent, ThrowDoHitEvent>(HandleThrow);
    }

    private void TryDoCollideStun(Entity<StunOnCollideComponent> ent, EntityUid target)
    {
        var knockedDown = _stunSystem.TryKnockdown(target, ent.Comp.KnockdownAmount, ent.Comp.Refresh, ent.Comp.AutoStand, ent.Comp.Drop, ent.Comp.Forced); // Starlight start - adding admin logging for taser knockdown effect

        if (knockedDown && TryComp<ProjectileComponent>(ent, out var projectile))
        {
            ent.Comp.Handled = true;
            _adminLogger.Add(LogType.DisarmedKnockdown,
                    LogImpact.Medium,
                    $"Projectile {ToPrettyString(ent):projectile} shot by {ToPrettyString(projectile.Shooter):user} from {ToPrettyString(projectile.Weapon):weapon} hit {ToPrettyString(target):target} and knocked them down");
        }
        // Starlight end

        if (ent.Comp.Refresh)
        {
            _stunSystem.TryUpdateStunDuration(target, ent.Comp.StunAmount);

            _movementMod.TryUpdateMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                ent.Comp.SlowdownAmount,
                ent.Comp.WalkSpeedModifier,
                ent.Comp.SprintSpeedModifier
            );
        }
        else
        {
            _stunSystem.TryAddStunDuration(target, ent.Comp.StunAmount);
            _movementMod.TryAddMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                ent.Comp.SlowdownAmount,
                ent.Comp.WalkSpeedModifier,
                ent.Comp.SprintSpeedModifier
            );
        }
    }

    private void HandleCollide(Entity<StunOnCollideComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.Handled) // Starlight addition
            return;

        if (args.OurFixtureId != ent.Comp.FixtureID)
            return;

        TryDoCollideStun(ent, args.OtherEntity);
    }

    private void HandleThrow(Entity<StunOnCollideComponent> ent, ref ThrowDoHitEvent args)
    {
        TryDoCollideStun(ent, args.Target);
    }
}
