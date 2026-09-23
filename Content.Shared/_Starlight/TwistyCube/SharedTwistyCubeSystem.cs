namespace Content.Shared._Starlight.TwistyCube;

public abstract partial class SharedTwistyCubeSystem : EntitySystem
{
    [Dependency] protected SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TwistyCubeComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    private void OnUIOpened(Entity<TwistyCubeComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_uiSystem.IsUiOpen(ent.Owner, TwistyCubeUiKey.Key))
            _uiSystem.SetUiState(ent.Owner, TwistyCubeUiKey.Key, new TwistyCubeBoundUserInterfaceState(ent.Comp.State));
    }
}