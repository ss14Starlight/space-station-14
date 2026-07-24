using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.StationRadio.Events;

[Serializable, NetSerializable]
public sealed class StationRadioMediaStoppedEvent : EntityEventArgs
{
    public StationRadioMediaStoppedEvent()
    {

    }
}
