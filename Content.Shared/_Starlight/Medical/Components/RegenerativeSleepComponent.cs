using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Medical.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RegenerativeSleepComponent : Component
{
    /// <summary>
    /// Time between healing updates, in seconds.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The next absolute game-time timestamp when healing may occur.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextHeal;

    /// <summary>
    /// Positive damage amounts to heal per update; the system negates these values when applying them.
    /// </summary>
    [DataField]
    public DamageSpecifier HealingPerUpdate = new();

    /// <summary>
    /// Positive bleed amount to heal per update; the system negates this value when applying it.
    /// </summary>
    [DataField]
    public float BleedHealPerUpdate = 0.5f;
}
