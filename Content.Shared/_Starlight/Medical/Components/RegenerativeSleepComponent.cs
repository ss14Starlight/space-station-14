using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Medical.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RegenerativeSleepComponent : Component
{
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextHeal;

    [DataField]
    public DamageSpecifier HealingPerUpdate = new();

    [DataField]
    public float BleedHealPerUpdate = 0.5f;
}
