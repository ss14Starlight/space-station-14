using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Magic.Events;

public sealed partial class TowerOfBabelEvent : InstantActionEvent
{}

[Serializable, NetSerializable]
public enum TowerOfBabelLayers : byte
{ Tower, }