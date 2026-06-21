namespace Content.Shared._Starlight.Abstract;

[RegisterComponent]
public sealed partial class RuleOwnerComponent : Component
{
    [DataField]
    public EntityUid RuleOwner;
}
