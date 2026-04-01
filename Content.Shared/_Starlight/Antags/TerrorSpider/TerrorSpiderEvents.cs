using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Antags.TerrorSpider;

#region Events

public sealed partial class EggInjectionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class EggInjectionDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class EggsLayingEvent : InstantActionEvent;

public sealed class EggsInjectedEvent : EntityEventArgs;

public sealed partial class AcidVentEvent : EntityTargetActionEvent;

#endregion