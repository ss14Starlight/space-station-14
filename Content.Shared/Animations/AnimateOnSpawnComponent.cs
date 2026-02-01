using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Animations;

[RegisterComponent]
public sealed partial class AnimateOnSpawnComponent : Component
{
    [DataField] public string AnimationState = "transform";
}

[Serializable, NetSerializable]
public enum AnimateOnSpawnVisualState : byte
{
    Animating,
}

[Serializable, NetSerializable]
public enum AnimateOnSpawnVisualLayers : byte
{
    Animation,
}