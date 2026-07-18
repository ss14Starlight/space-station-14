using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Engineering;

[Serializable, NetSerializable]
public sealed partial class SpawnAfterInteractDoAfterEvent : DoAfterEvent
{
    [DataField("coordinates", required: true)]
    public NetCoordinates ClickLocation;

    private SpawnAfterInteractDoAfterEvent()
    {
    }

    public SpawnAfterInteractDoAfterEvent(NetCoordinates clickLocation)
    {
        ClickLocation = clickLocation;
    }

    public override DoAfterEvent Clone() => this;
}
