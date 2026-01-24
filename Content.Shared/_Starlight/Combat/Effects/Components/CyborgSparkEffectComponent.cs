using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Combat.Effects.Components;

/// <summary>
/// Component for cyborgs that triggers spark effects when hit by any hitscan bullets.
/// Unlike ArmorSparkEffectComponent, this triggers on any bullet hit regardless of type or armor values.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgSparkEffectComponent : Component
{
    /// <summary>
    /// The prototype ID of the spark effect entity to spawn.
    /// </summary>
    [DataField("sparkEffectPrototype")]
    public EntProtoId SparkEffectPrototype = "ArmorSparkEffect";

    /// <summary>
    /// The sound collection to play when sparks are triggered.
    /// </summary>
    [DataField("ricochetSoundCollection")]
    public ProtoId<SoundCollectionPrototype> RicochetSoundCollection = "armor_ricochet_cyborg";

    /// <summary>
    /// Maximum random offset in X and Y directions for spark positioning.
    /// </summary>
    [DataField("maxOffset")]
    public float MaxOffset = 0.3f;
}
