using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Examine;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.TwistyCube;

public sealed partial class TwistyCubeSystem: EntitySystem
{
    private const int ScrambleMoves = 60;

    [Dependency] private SharedUserInterfaceSystem _uiSystem = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IGameTiming _timing = null!;

    [SubscribeLocalEvent]
    private void OnExamined(Entity<TwistyCubeComponent> ent, ref ExaminedEvent args)
    {
        var solvedText = ent.Comp.State.Solved ? Loc.GetString("twistycube-solved") : Loc.GetString("twistycube-unsolved");
        args.PushMarkup(solvedText);
    }

    [SubscribeLocalEvent]
    private void OnUIOpened(Entity<TwistyCubeComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_uiSystem.IsUiOpen(ent.Owner, TwistyCubeUiKey.Key))
            _uiSystem.SetUiState(ent.Owner, TwistyCubeUiKey.Key, new TwistyCubeBoundUserInterfaceState(ent.Comp.State));
    }

    [SubscribeLocalEvent]
    private void OnAfterHandleState(Entity<TwistyCubeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
    }

    [SubscribeLocalEvent]
    private void OnCubeAction(Entity<TwistyCubeComponent> ent, ref TwistyCubeActionMessage msg)
    {
        var action = msg.Action;
        var comp = ent.Comp;
        if (action == TwistyCubeAction.Scramble)
        {
            var netId = GetNetEntity(ent).Id;
            for (var i = 0; i < ScrambleMoves; i++)
                comp.State.ApplyAction((TwistyCubeAction) _random.NextPredictedRange(
                    _timing,
                    (int) TwistyCubeAction.FrontClockwise,
                    (int) TwistyCubeAction.Scramble,
                    SharedRandomExtensions.HashCodeCombine(netId, i)
                ));
        }
        else
            comp.State.ApplyAction(action);

        Dirty(ent, comp);
        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
        // send the new state to the BUI that sent the TwistyCubeActionMessage
        if (CompOrNull<UserInterfaceComponent>(ent) is {} uiComp)
            _uiSystem.SetUiState(new Entity<UserInterfaceComponent?>(ent.Owner, uiComp), msg.UiKey, new TwistyCubeBoundUserInterfaceState(comp.State));
    }
}
