using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Vehicle.Components;

public sealed partial class VehicleHandBlockerComponent
{
    /// <summary>
    /// Operators that control the vehicle without occupying physical hands. (Aka borgs)
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? ExemptOperators;
}
