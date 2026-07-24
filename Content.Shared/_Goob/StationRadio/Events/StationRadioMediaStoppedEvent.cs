using Robust.Shared.Serialization;

namespace Content.Shared._Goob.StationRadio.Events;

[Serializable, NetSerializable]
public sealed class StationRadioMediaStoppedEvent : EntityEventArgs
{
    public StationRadioMediaStoppedEvent()
    {

    }
}
