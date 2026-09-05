using Content.Server._Starlight.Cargo.MaterialDispenser;
using Content.Shared._Starlight.Cargo.MaterialDispenser;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chemistry;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Cargo.Components;

/// <summary>
/// This is used for marking a machine as a material dispenser to allow to spawn crates with materials in them.
/// </summary>
[RegisterComponent]
[Access(typeof(MaterialDispenserSystem))]
public sealed partial class MaterialDispenserComponent : Component
{
    [DataField("mode"), ViewVariables(VVAccess.ReadWrite)]
    public MaterialDispenserMode Mode = MaterialDispenserMode.Transfer;

    [DataField("buffer"), ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, int> Buffer = new();

    [DataField] public ProtoId<CargoAccountPrototype> Account = "Cargo";

    [DataField] public EntProtoId? CrateId { get; private set; } = "CrateGenericSteel";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RewardMultiplier = 0.1f;

    [DataField] public ProtoId<MaterialPrototype> CrateMaterial = "Steel";

    [DataField] public int CrateMaterialAmount = 5;

    [DataField] public EntProtoId TicketProtoId = "SalvageTicket";

}
