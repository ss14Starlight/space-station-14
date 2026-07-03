using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Clothing;

/// <summary>Do-after event for capacitor gloves power injection.</summary>
[Serializable, NetSerializable]
public sealed partial class CapacitorInjectDoAfterEvent : SimpleDoAfterEvent;
