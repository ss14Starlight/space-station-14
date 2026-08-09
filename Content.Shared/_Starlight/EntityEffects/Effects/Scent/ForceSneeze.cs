using Content.Shared.EntityEffects;

namespace Content.Shared._Starlight.EntityEffects.Effects.Scent;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ForceSneeze : EntityEffectBase<ForceSneeze>
{
    [DataField]
    public TimeSpan Lockout = TimeSpan.FromSeconds(12);
}
