using Content.Shared.Nutrition.Components;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;

namespace Content.Server.Nutrition.EntitySystems;

public sealed partial class CreamPieSystem
{
    [Dependency] private SharedStunSystem _stunSystem = default!;

    private partial void OnProjectilePieHit(Entity<CreamPieComponent> entity, ref ProjectileHitEvent args)
    {
        if (TryComp<CreamPiedComponent>(args.Target, out var creamPied))
        {
            SetCreamPied(args.Target, creamPied, true);
            _stunSystem.TryUpdateParalyzeDuration(args.Target, TimeSpan.FromSeconds(entity.Comp.ParalyzeTime));
        }

        SplatCreamPie(entity);
    }
}
