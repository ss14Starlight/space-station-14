using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.StationRadio.Events; // Starlight - _Goob -> _Goobstation

[Serializable, NetSerializable]
public sealed class StationRadioMediaStoppedEvent : EntityEventArgs
{
    public StationRadioMediaStoppedEvent()
    {

    }
}
