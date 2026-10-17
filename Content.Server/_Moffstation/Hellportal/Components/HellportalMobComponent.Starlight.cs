
namespace Content.Server._Moffstation.Hellportal.Components;

public sealed partial class HellportalMobComponent : Component
{
    /// <summary>
    /// Portal whose spawn limit this mob counts towards. Unowned mobs do not use any portal's quota.
    /// </summary>
    [DataField]
    public EntityUid? SourcePortal;
}
