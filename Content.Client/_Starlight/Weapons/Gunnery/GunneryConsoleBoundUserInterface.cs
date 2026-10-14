using Content.Shared._Starlight.Weapons.Gunnery;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Weapons.Gunnery;

[UsedImplicitly]
public sealed class GunneryConsoleBoundUserInterface : BoundUserInterface
{
    private GunneryConsoleWindow? _window;

    public GunneryConsoleBoundUserInterface(EntityUid owner, Enum uiKey)
        : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<GunneryConsoleWindow>();

        _window.OnFireRequested = (cannon, target) =>
            // Convert EntityCoordinates → NetCoordinates for the network message.
            SendPredictedMessage(new GunneryConsoleFireMessage
            {
                Cannon = cannon,
                Target = EntMan.GetNetCoordinates(target),
            });

        _window.OnGuidanceUpdate = target =>
            // Guidance messages are not predicted — they steer a physics body server-side.
            SendMessage(new GunneryConsoleGuidanceMessage
            {
                Target = EntMan.GetNetCoordinates(target),
            });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not GunneryConsoleBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(cState);
    }
}
