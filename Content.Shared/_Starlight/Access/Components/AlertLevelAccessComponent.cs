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
    /// The alert level. Cleared when unanchored or not on a station-affiliated grid.
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

    /// <summary>
    /// Contains access lists that will be added, grouped by alert level.
    /// </summary>
    [DataField("added"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<string, List<HashSet<ProtoId<AccessLevelPrototype>>>> AddedAccesses = new();

    /// <summary>
    /// Contains access lists that will be removed, grouped by alert level.
    /// </summary>
    [DataField("removed"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<string, List<HashSet<ProtoId<AccessLevelPrototype>>>> RemovedAccesses = new();

    #endregion
}
