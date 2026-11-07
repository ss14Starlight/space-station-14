using Content.Shared._Starlight.Atmos.AirlockCycler;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Atmos.AirlockCycler;

[UsedImplicitly]
public sealed class AirlockCyclerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AirlockCyclerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AirlockCyclerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnCycle += side => SendMessage(new AirlockCyclerCycleMessage(side));
        _window.OnForce += () => SendMessage(new AirlockCyclerForceMessage());
        _window.OnLockdown += () => SendMessage(new AirlockCyclerLockdownMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AirlockCyclerBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
