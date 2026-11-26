using Content.Shared.Database;

// ReSharper disable CheckNamespace
namespace Content.Shared.Explosion.Components;

public sealed partial class ExplosiveComponent
{
    /// <summary>
    /// Overrides the impact assigned to this explosive's admin log. If unset, normal explosion impact rules apply.
    /// </summary>
    [DataField]
    public LogImpact? AdminLogImpact;
}
