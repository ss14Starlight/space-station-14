using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class InfernalContractComponent : Component
{
    [AutoNetworkedField]
    public EntityUid Author;

    [AutoNetworkedField]
    public bool Completed = false;

    [AutoNetworkedField]
    public EntityUid? Signator = null;

    /// <summary>
    /// What does the text of the blank clauses contain?
    /// Used to seperate empty clauses (irrelevant) from mispelled clauses (relevant)
    /// </summary>
    [DataField]
    public string BlankClauseText = "[form]";

    /// <summary>
    /// Name is changed when there is a misprint
    /// </summary>
    [DataField]
    public LocId MispelledContractName = "infernal-contract-misspelled-name";

    [DataField]
    public LocId CorrectContractName= "infernal-contract-valid-name";
}
