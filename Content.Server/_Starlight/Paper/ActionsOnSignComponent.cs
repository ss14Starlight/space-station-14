using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper;

[RegisterComponent]
public sealed partial class ActionsOnSignComponent : Component
{
    [DataField("actions", required: true)]
    public ProtoId<OnSignActionsPrototype> OnSignActionProto = default!;

    /// <summary>
    /// whether the actions should be carried out instantly upon signing or after all the charges have been consumed
    /// </summary>
    [DataField]
    public bool Instant = true;

    /// <summary>
    /// how many uses the paper has before it is out. if Instant is false it will execute the actions instantly.
    /// </summary>
    [DataField]
    public int Charges = 1;
    
    /// <summary>
    /// A set of every entity that signed this paper. both to prevent re-signing and to deploy the effects once all signatures are collected
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Signers = new();
    
    /// <summary>
    /// is the faxable component kept? this is for admeme protos
    /// </summary>
    [DataField]
    public bool KeepFaxable = false;
}