using Robust.Shared.Audio;

// ReSharper disable once CheckNamespace
namespace Content.Shared._FarHorizons.Tools.FloorBuffer.Components;

public sealed partial class FloorBufferComponent : Component
{
    [AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public SoundSpecifier? ActiveSound;

    public EntityUid? SoundStream;
}
