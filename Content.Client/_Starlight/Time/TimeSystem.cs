using Content.Shared._Starlight.Time;

namespace Content.Client._Starlight.Time;

public sealed partial class TimeSystem : SharedTimeSystem
{
    public override void Initialize()
    {
        base.Initialize();
        RaiseNetworkEvent(new RequestRoundDateEvent());
    }
}
