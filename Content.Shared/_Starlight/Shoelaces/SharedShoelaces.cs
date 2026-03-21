using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shoelaces;

[Serializable, NetSerializable]
public sealed partial class ShoelaceTieDoAfterEvent : SimpleDoAfterEvent
{
    public bool Together { get; }
    public ShoelaceTieDoAfterEvent (bool together) => Together = together;
}

[Serializable, NetSerializable]
public sealed partial class ShoelaceUntieDoAfterEvent : SimpleDoAfterEvent
{
    public bool SelfUntie { get; }

    public ShoelaceUntieDoAfterEvent (bool selfUntie) => SelfUntie = selfUntie;
}

public sealed partial class RemoveTiedShoelacesAlertEvent : BaseAlertEvent;