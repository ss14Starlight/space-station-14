using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Starlight.Utility;

[Serializable, NetSerializable]
[DataDefinition, Virtual]
public partial class ExtendedSpriteSpecifier
{

    /// <summary>
    /// Basic SpriteSpecifier
    /// </summary>
    [DataField]
    public SpriteSpecifier Sprite { get; internal set; }

    /// <summary>
    /// Sprite Color(Additional)
    /// </summary>
    [DataField("color")]
    public Color SpriteColor = Color.White;

    [DataField("scale")]
    public Vector2 SpriteScale = new(1, 1);

    [DataField("noRot")]
    public bool SpriteRotation = true;

    [DataField]
    public Vector2 Offset = Vector2.Zero;

    public override bool Equals(object? obj)
        => obj is ExtendedSpriteSpecifier other
            && Sprite.Equals(other.Sprite)
            && SpriteColor == other.SpriteColor
            && SpriteScale == other.SpriteScale
            && Offset == other.Offset
            && SpriteRotation == other.SpriteRotation;

    public override int GetHashCode() => HashCode.Combine(Sprite, SpriteColor, SpriteScale, SpriteRotation, Offset);
}
