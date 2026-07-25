using Content.Shared._Starlight.Traits.Assorted;
using Content.Shared.Cloning.Events;

namespace Content.Server._Starlight.Traits.Assorted;

/// <summary>
/// Handles the Unclonable trait, preventing affected entities from being cloned.
/// The cloning pod console will announce an incompatible DNA error when this fires.
/// </summary>
public sealed class UncloneableTraitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UncloneableComponent, CloningAttemptEvent>(OnCloningAttempt);
    }

    private void OnCloningAttempt(Entity<UncloneableComponent> ent, ref CloningAttemptEvent args) => args.Cancelled = true;
}
