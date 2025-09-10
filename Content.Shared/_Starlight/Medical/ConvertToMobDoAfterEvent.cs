using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared._Starlight.Medical;

[NetSerializable, Serializable]
public sealed partial class ConvertToMobDoAfterEvent : SimpleDoAfterEvent { }