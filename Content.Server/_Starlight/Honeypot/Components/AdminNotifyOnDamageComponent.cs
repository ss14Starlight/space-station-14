using Content.Shared.Damage.Components;

namespace Content.Server._Starlight.Honeypot.Components;

/// <summary>
/// Turns an entity with a <see cref="DamageableComponent"/> into a honeypot that notifies admins on damage.
/// </summary>
[RegisterComponent]
public sealed partial class AdminNotifyOnDamageComponent : Component
{
    /// <summary>
    /// What to call the subject in the admin notification.
    /// </summary>
    [DataField] public string Subject = "entity";
}
