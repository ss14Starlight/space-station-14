using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Standing;

namespace Content.Server.Silicons.Borgs;

/// <summary>
/// Cancels crit/death item-drop throws for items provided by borg modules.
/// Items with <see cref="BorgHand.ForceRemovable"/> (e.g. the contraband bag) intentionally
/// do NOT get this component, so they continue to drop normally on incapacitation.
/// </summary>
public sealed class BorgModuleItemSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgModuleItemComponent, FellDownThrowAttemptEvent>(OnFellDown);
    }

    private void OnFellDown(Entity<BorgModuleItemComponent> ent, ref FellDownThrowAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
