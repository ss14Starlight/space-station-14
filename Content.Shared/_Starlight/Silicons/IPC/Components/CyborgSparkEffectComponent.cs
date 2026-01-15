// Cyborg Spark Effect Component
// SOURCE: Far-Horizons-SS14 (Starlight Upstream)
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14
// _STARLIGHT: Ported from upstream for IPC spark effects on damage

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons.IPC.Components;

/// <summary>
/// Component for cyborgs/IPCs that triggers spark effects when hit by any hitscan bullets.
/// Unlike ArmorSparkEffectComponent, this triggers on any bullet hit regardless of type or armor values.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgSparkEffectComponent : Component
{
    /// <summary>
    /// The prototype ID of the spark effect entity to spawn.
    /// </summary>
    [DataField("sparkEffectPrototype")]
    public string SparkEffectPrototype = "EffectSparks";

    /// <summary>
    /// The sound to play when sparks are triggered.
    /// </summary>
    [DataField("sparkSound")]
    public SoundSpecifier SparkSound = new SoundPathSpecifier("/Audio/Effects/sparks1.ogg");

    /// <summary>
    /// Maximum random offset in X and Y directions for spark positioning.
    /// </summary>
    [DataField("maxOffset")]
    public float MaxOffset = 0.3f;
}
