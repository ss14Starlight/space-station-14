using Content.Server._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [SubscribeLocalEvent]
    private void OnCollectiveSpeechRelay(BodyControlSpeechRelayEvent args)
    {
        if (args.Message.StartsWith("+h ", StringComparison.OrdinalIgnoreCase))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnCollectivePersonalInterface(BodyControlPersonalInterfaceEvent args)
    {
        if (args.UiKey is MindCollectiveUiKey)
            args.Personal = true;
    }
}
