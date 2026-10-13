namespace Content.Shared.Mind;

public sealed partial class RoleTypePrototype
{
    /// <summary>
    /// Default faction shown in the character menu when the mind has no faction subtype.
    /// </summary>
    [DataField]
    public LocId? Faction;

    /// <summary>
    /// Character menu alignment label, defaulting to the role type's name.
    /// </summary>
    [DataField]
    public LocId? Alignment;
}
