using Content.Shared._Starlight.Traits;
using Content.Shared._Starlight.Traits.Assorted;
using Content.Shared.Cloning.Events;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Traits.Assorted;

/// <summary>
/// Handles the Unclonable trait, preventing affected entities from being cloned.
/// </summary>
public sealed class UncloneableTraitSystem : EntitySystem
{
    private static readonly ProtoId<TraitPrototype> _traitId = "Unclonable";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<UncloneableComponent, CloningAttemptEvent>(OnCloningAttempt);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.Profile.TraitPreferences.Contains(_traitId))
            EnsureComp<UncloneableComponent>(args.Mob);
    }

    private void OnCloningAttempt(Entity<UncloneableComponent> ent, ref CloningAttemptEvent args) => args.Cancelled = true;
}
