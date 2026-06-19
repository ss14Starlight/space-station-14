using Robust.Shared.GameStates;

namespace Content.Server._Starlight.Cargo.TamperSeal;

/// <summary>
/// Marker component that makes a tamper-sealed container tracked for integrity performance purposes.
/// Despite the name there is no actual radio or examine text involved due to scope creep being a thing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TamperSealIntegrityBeaconComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid StationId;
}
