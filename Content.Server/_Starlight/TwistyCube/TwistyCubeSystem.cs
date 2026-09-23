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
    }
}