namespace Content.Shared._Starlight.Containers;

public abstract partial class CannotStorageInsertSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CannotStorageInsertComponent, ContainerGettingInsertedAttemptEvent>(OnAttemptInsert);
    }

    public void OnAttemptInsert(Entity<CannotStorageInsertComponent> ent, ref ContainerGettingInsertedAttemptEvent ev)
    {
        ev.Cancel();
    }
}
