using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.Components;

/// <summary>
/// Print a barcode label that can be attached to an item to redirect sale proceeds to a department.
/// </summary>
[Serializable, NetSerializable]
public sealed class CargoConsolePrintBarcodeMessage(ProtoId<CargoAccountPrototype> account) : BoundUserInterfaceMessage
{
    public ProtoId<CargoAccountPrototype> Account = account;
}
