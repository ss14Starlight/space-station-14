// ReSharper disable CheckNamespace
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Ninja.Systems;

/// <summary>Drains power into the worn gloves' cell.</summary>
[Serializable, NetSerializable]
public sealed partial class GloveDrainDoAfterEvent : SimpleDoAfterEvent;
