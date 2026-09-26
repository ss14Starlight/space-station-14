using Content.Shared.Examine;

namespace Content.Shared._Starlight.TwistyCube;

public sealed partial class TwistyCubeSystem: EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TwistyCubeComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<TwistyCubeComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<TwistyCubeComponent, TwistyCubeActionMessage>(OnCubeAction);
        SubscribeLocalEvent<TwistyCubeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<TwistyCubeComponent> ent, ref ExaminedEvent args)
    {
        var isSolved = ent.Comp.State == new TwistyCubeState();
        var solvedText = isSolved ? Loc.GetString("twistycube-solved") : Loc.GetString("twistycube-unsolved");

        args.PushMarkup(solvedText);
    }

    private void OnUIOpened(Entity<TwistyCubeComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_uiSystem.IsUiOpen(ent.Owner, TwistyCubeUiKey.Key))
            _uiSystem.SetUiState(ent.Owner, TwistyCubeUiKey.Key, new TwistyCubeBoundUserInterfaceState(ent.Comp.State));
    }

    private void OnAfterHandleState(Entity<TwistyCubeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
    }

    private void OnCubeAction(Entity<TwistyCubeComponent> ent, ref TwistyCubeActionMessage msg)
    {
        TwistyCubeAction action = msg.Action;
        TwistyCubeComponent comp = ent.Comp;
        comp.State.ApplyAction(action);
        Dirty(ent, comp);
        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
        // send the new state to the BUI that sent the TwistyCubeActionMessage
        if (CompOrNull<UserInterfaceComponent>(ent) is {} uiComp)
            _uiSystem.SetUiState(new Entity<UserInterfaceComponent?>(ent.Owner, uiComp), msg.UiKey, new TwistyCubeBoundUserInterfaceState(comp.State));
    }
}