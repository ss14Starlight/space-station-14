using Content.Shared.Damage;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Medical.Damage;

/// <summary>
/// Raised after damage resistances and global modifiers, immediately before damage is applied.
/// </summary>
public sealed class DamageAfterModifiersEvent : EntityEventArgs
{
    public required DamageSpecifier Damage;
    public EntityUid? Origin;
}
