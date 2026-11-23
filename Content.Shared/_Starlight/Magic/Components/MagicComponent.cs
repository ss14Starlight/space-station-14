// ReSharper disable CheckNamespace
// Partial of the upstream MagicComponent, so it has to share its namespace.
namespace Content.Shared.Magic.Components;
public sealed partial class MagicComponent : Component
{
    /// <summary>
    ///     Is this spell aggressive?
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsAggressive = true;
}
