using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Clothing;

/// <summary>Do-after event for capacitor gloves power injection.</summary>
[Serializable, NetSerializable]
public sealed partial class CapacitorInjectDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Operating mode for capacitor gloves.</summary>
[Serializable, NetSerializable]
public enum CapacitorGlovesMode : byte
{
    /// <summary>Hand-interacting with a power device drains it into the cell.</summary>
    Drain,
    /// <summary>Hand-interacting with a power device injects charge from the cell into it.</summary>
    Inject,
}
