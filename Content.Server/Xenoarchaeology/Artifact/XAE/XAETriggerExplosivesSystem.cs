using Content.Server._Starlight.Achievement;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Xenoarchaeology.Artifact.XAE.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;

namespace Content.Server.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// System for xeno artifact effect of triggering explosion.
/// </summary>
public sealed partial class XAETriggerExplosivesSystem : BaseXAESystem<XAETriggerExplosivesComponent>
{
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private AchievementSystem _achievements = default!; // Starlight-edit

    /// <inheritdoc />
    protected override void OnActivated(Entity<XAETriggerExplosivesComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        if(!TryComp<ExplosiveComponent>(ent, out var explosiveComp))
            return;
        if (args.User.HasValue) _achievements.QueueUnlockAchievement(args.User.Value, "sci_exploded"); // Starlight-edit

        _explosion.TriggerExplosive(ent, explosiveComp);
    }
}
