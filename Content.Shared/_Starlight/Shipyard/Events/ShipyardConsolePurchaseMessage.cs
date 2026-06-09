using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shipyard.Events;

/// <summary>
///     Purchase a Vessel from the console
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsolePurchaseMessage : BoundUserInterfaceMessage
{
    /// <summary>Vessel prototype ID.</summary>
    public string Vessel { get; }

    public ShipyardConsolePurchaseMessage(string vessel) =>
        Vessel = vessel;
}
