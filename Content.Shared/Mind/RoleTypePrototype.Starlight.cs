namespace Content.Shared.Mind;

public sealed partial class RoleTypePrototype
{
    /// <summary>
    /// Default faction shown in the character menu when the mind has no faction subtype.
    /// </summary>
    [DataField]
    public LocId? Faction;

    /// <summary>
    /// Default faction color when the active mind role does not specify one.
    /// </summary>
    [DataField]
    public Color? FactionColor;

    /// <summary>
    /// Character menu alignment label, defaulting to the role type's name.
    /// </summary>
    [DataField]
    public LocId? Alignment;
}
