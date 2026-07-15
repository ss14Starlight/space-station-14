using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Ninja;

/// <summary>
/// Only exists in shared to provide API and for access.
/// All logic is serverside.
/// </summary>
public abstract class SharedPodHackerSystem : EntitySystem
{
}

/// <summary>
/// DoAfter event for pod console extract ability.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ExtractDoAfterEvent : SimpleDoAfterEvent { }
