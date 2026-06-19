namespace Content.Server._Starlight.Cargo.TamperSeal.Components;

/// <summary>
/// Marker component that makes a tamper-sealed container tracked for integrity performance purposes.
/// Despite the name, there is no actual radio or examine text involved, because scope creep.
/// </summary>
[RegisterComponent]
public sealed partial class TamperSealIntegrityBeaconComponent : Component
{
    /// <summary>
    /// The station that this beacon reports to.
    /// </summary>
    public EntityUid StationId;
}
