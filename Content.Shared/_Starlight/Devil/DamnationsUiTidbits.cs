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
    public readonly List<ProtoId<DamnationPrototype>> Damnations;

    public DevilDamnationsBuiState(List<ProtoId<DamnationPrototype>> damnations) => Damnations = new(damnations);
}
