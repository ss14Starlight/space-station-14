using Content.Server._Starlight.BodyControl;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [SubscribeLocalEvent]
    private void OnCollectiveSpeechRelay(BodyControlSpeechRelayEvent args)
    {
        if (args.Message.StartsWith("+h ", StringComparison.OrdinalIgnoreCase))
            args.Cancelled = true;
    }
}
