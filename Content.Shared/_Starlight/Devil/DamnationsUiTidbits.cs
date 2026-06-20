using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Devil;

[Serializable, NetSerializable]
public enum DamnationsMenuUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed partial class DevilDamnationsBuiState : BoundUserInterfaceState
{
    // damnation prototypes, and number of times they were used
    public readonly List<(ProtoId<DamnationPrototype>, int)> Damnations;

    public DevilDamnationsBuiState(List<(ProtoId<DamnationPrototype>, int)> damnations) => Damnations = new(damnations);
}
