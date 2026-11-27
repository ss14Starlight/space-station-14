using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Actions.Events;

public sealed partial class FelionoidPounceActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class FelionoidPounceDoAfterEvent : SimpleDoAfterEvent;
