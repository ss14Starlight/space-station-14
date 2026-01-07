using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared._FarHorizons.Vehicles;

[Serializable, NetSerializable]
public sealed partial class VehicleRemoveDoAfter : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VehicleEntryDoAfter : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VehicleUnbuckleDoAfter : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TurnKeysDoAfter : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class EjectKeysDoAfter : SimpleDoAfterEvent;

public sealed partial class TurnKeysEvent : InstantActionEvent;
public sealed partial class HornActionEvent : InstantActionEvent;
public sealed partial class ToggleTrunkActionEvent : InstantActionEvent;