using Content.Shared._Starlight.StationGoals;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.StationGoals;

public sealed class StationGoalConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private StationGoalConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StationGoalConsoleWindow>();
        _window.OnStartVote += () => SendMessage(new StationGoalStartVoteMessage());
        _window.OnVote += goal => SendMessage(new StationGoalVoteMessage(goal));
        _window.OnPrint += () => SendMessage(new StationGoalPrintMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is StationGoalConsoleState goalState)
            _window?.UpdateState(goalState);
    }
}
