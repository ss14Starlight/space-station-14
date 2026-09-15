using Content.Shared._Starlight.IdentityManagement.Components;
using Content.Shared.Silicons.Borgs.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.IdentityManagement;

public sealed partial class IdentitySystem
{
    /// <summary>
    /// Whether this entity's real name should always show, ignoring any
    /// identity-concealing worn items. Borgs are always identifiable (chassis
    /// shape aside, e.g. Borgi); anything with <see cref="AlwaysIdentifiableComponent"/>
    /// opts into the same behavior.
    /// </summary>
    private bool IsAlwaysIdentifiable(EntityUid target)
    {
        return HasComp<BorgChassisComponent>(target)
            || HasComp<AlwaysIdentifiableComponent>(target);
    }
}
