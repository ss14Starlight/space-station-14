using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Turrets;

public sealed partial class TurretTargetSettingsSystem
{
    [PublicAPI]
    public void SetTargetNeutralEntities(Entity<TurretTargetSettingsComponent?> ent, bool enabled)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.TargetNeutralEntities = enabled;
        Dirty(ent);
    }
}
