using Content.Server.GameTicking;
using Content.Shared.Eye;
using Content.Shared.Revenant.Components;
using Content.Shared.Revenant.EntitySystems;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;

namespace Content.Server.Revenant.EntitySystems;

public sealed partial class CorporealSystem : SharedCorporealSystem
{
    [Dependency] private VisibilitySystem _visibilitySystem = default!;
    [Dependency] private GameTicker _ticker = default!;

    public override void OnApplied(Entity<CorporealComponent> effect, ref StatusEffectAppliedEvent args)
    {
        base.OnApplied(effect, ref args);

        var uid = args.Target;
        if (TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibilitySystem.RemoveLayer((uid, visibility), (int) VisibilityFlags.NullSpace, false);
            _visibilitySystem.AddLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(uid, visibility);
        }
    }

    public override void OnRemoved(Entity<CorporealComponent> effect, ref StatusEffectRemovedEvent args)
    {
        base.OnRemoved(effect, ref args);

        var uid = args.Target;
        if (TryComp<VisibilityComponent>(uid, out var visibility) && _ticker.RunLevel != GameRunLevel.PostRound)
        {
            _visibilitySystem.AddLayer((uid, visibility), (int) VisibilityFlags.NullSpace, false);
            _visibilitySystem.RemoveLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(uid, visibility);
        }
    }
}
