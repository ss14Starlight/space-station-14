using Content.Shared._Starlight.TwistyCube;
using Content.Shared.UserInterface;

namespace Content.Server._Starlight.TwistyCube;

public sealed partial class TwistyCubeSystem: SharedTwistyCubeSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TwistyCubeComponent, TwistyCubeActionMessage>(OnCubeAction);
    }

    private void OnCubeAction(Entity<TwistyCubeComponent> ent, ref TwistyCubeActionMessage msg)
    {
        TwistyCubeAction action = msg.Action;
        TwistyCubeComponent comp = ent.Comp;
        comp.State.ApplyAction(action);
        Dirty(ent, comp);
        // send the new state to the BUI that sent the TwistyCubeActionMessage
        if (CompOrNull<UserInterfaceComponent>(ent) is {} uiComp)
            _uiSystem.SetUiState(new Entity<UserInterfaceComponent?>(ent.Owner, uiComp), msg.UiKey, new TwistyCubeBoundUserInterfaceState(comp.State));
    }
}