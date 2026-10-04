using Content.Shared.Access;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Access.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class AlertLevelAccessComponent : Component
{
    #region State

    /// <summary>
    /// The alert level color. Cleared when unanchored or not on a station-affiliated grid.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public string? Level;

    /// <summary>
    /// The alert level color. Cleared when unanchored or not on a station-affiliated grid.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public Color LevelColor = Color.White;

    #endregion
    #region Access configuration

    [DataField("added"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<string, HashSet<ProtoId<AccessLevelPrototype>>> AddedAccesses = new();

    /// <summary>
    /// Contains accesses that are actively removed from access lists.
    /// </summary>
    [DataField("removed"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<string, HashSet<ProtoId<AccessLevelPrototype>>> RemovedAccesses = new();

    #endregion
}
