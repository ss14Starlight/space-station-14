using Content.Shared._Starlight.StatusIcon;

namespace Content.Shared.Access.Systems;

public abstract partial class SharedJobStatusSystem : EntitySystem
{
    private void OnFixedJobIconStartup(Entity<FixedJobIconComponent> ent, ref ComponentStartup args)
    {
        UpdateStatus(ent.Owner);
    }
}
