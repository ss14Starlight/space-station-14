using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleIgnitionComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionTurnKeys";

    [AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public TimeSpan StartupTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan KeyRemovalTime = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? StartupSound;

    [AutoNetworkedField]
    public bool Started;
}

[ByRefEvent]
public readonly record struct VehicleIgnitionChangedEvent(bool Started);

public sealed partial class TurnKeysEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class TurnKeysDoAfter : SimpleDoAfterEvent
{
    public readonly bool Start;

    public TurnKeysDoAfter(bool start)
        => Start = start;
}

[Serializable, NetSerializable]
public sealed partial class EjectVehicleKeyDoAfter : SimpleDoAfterEvent
{
    public readonly NetEntity Key;

    public EjectVehicleKeyDoAfter(NetEntity key)
        => Key = key;
}
