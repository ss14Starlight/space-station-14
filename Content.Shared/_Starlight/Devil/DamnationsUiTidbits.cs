using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Devil;

[Serializable, NetSerializable]
public enum DamnationsMenuUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed partial class DevilDamnationsBuiState(List<(ProtoId<DamnationPrototype>, int)> damnations, List<(NetEntity, string)> damnedEntities) : BoundUserInterfaceState
{
    /// <summary>
    /// damnation prototypes, and number of times they were used
    /// </summary>
    public readonly List<(ProtoId<DamnationPrototype>, int)> Damnations = damnations;

    /// <summary>
    /// list of damned crew uids and names
    /// </summary>
    public readonly List<(NetEntity, string)> DamnedEntities = damnedEntities;
}
