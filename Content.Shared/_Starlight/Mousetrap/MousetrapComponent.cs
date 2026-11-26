// ReSharper disable CheckNamespace
namespace Content.Shared.Mousetrap;

public sealed partial class MousetrapComponent
{
    /// <summary>
    /// Upper limit for the mass-based damage multiplier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxDamageMultiplier = 15f;
}
