namespace Content.Shared.Roles.Components;

public sealed partial class MindRoleComponent
{
    /// <summary>
    /// Optional character menu name, overriding the antagonist prototype's name.
    /// </summary>
    [DataField]
    public LocId? CharacterMenuName;
}
