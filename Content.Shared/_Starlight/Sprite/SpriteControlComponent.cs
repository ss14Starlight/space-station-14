using System.Numerics;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Polymorph.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Sprite;

/// <summary>
/// Replace the sprite of the attached entity, better than using <see cref="ChameleonDisguiseComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SpriteControlComponent : Component
{
    /// <summary>
    /// Live entity to copy the sprite from.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? SourceEntity;

    /// <summary>
    /// Prototype to copy the sprite from. Also used as a fallback if <see cref="SourceEntity"/> isn't available.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId? SourceProto;

    /// <summary>
    /// Set entity to use specific sprite specifier.
    /// </summary>
    /// <remarks>
    /// Disabled if you are a ghost and don't have +Bypass.
    /// </remarks>
    [DataField, AutoNetworkedField] public SpriteSpecifier? Sprite;

    /// <summary>
    /// If set, the sprite of <see cref="SourceEntity"/> is re-copied this often (in seconds), so the copy follows
    /// its live appearance (toggles, damage, animations swapping states...). Null copies it once.
    /// </summary>
    [DataField, AutoNetworkedField] public float? MirrorInterval;

    /// <summary>
    /// Keep drawing worn clothing on top of the replacement sprite.
    /// </summary>
    [DataField, AutoNetworkedField] public bool KeepClothing;

    /// <summary>
    /// Keep drawing held items on top of the replacement sprite.
    /// </summary>
    [DataField, AutoNetworkedField] public bool KeepHeld;

    /// <summary>
    /// Hide layers that other systems add to this entity while it uses a replacement sprite (its own visualizers,
    /// markings, effects...). Without this they get drawn on top of the replacement.
    /// </summary>
    [DataField, AutoNetworkedField] public bool HideOwnLayers = true;

    [DataField, AutoNetworkedField] public Vector2? Offset;

    [DataField, AutoNetworkedField] public Angle? Rotation;

    [DataField, AutoNetworkedField] public Color? Color;

    [DataField, AutoNetworkedField] public int? DrawDepth;

    [DataField, AutoNetworkedField] public bool? NoRotation;

    [ViewVariables] public EntityUid? Backup;

    [ViewVariables] public SpriteControlConfig? Applied;

    [ViewVariables] public bool Replaced;

    [ViewVariables] public bool UsedLiveSource;

    [ViewVariables] public TimeSpan NextMirror;

    [ViewVariables] public readonly HashSet<object> SourceLayers = new(ReferenceEqualityComparer.Instance);
}

/// <summary>
/// Everything on <see cref="SpriteControlComponent"/> that requires rebuilding the sprite when it changes.
/// </summary>
public readonly record struct SpriteControlConfig(
    EntityUid? SourceEntity,
    EntProtoId? SourceProto,
    SpriteSpecifier? Sprite,
    float? MirrorInterval,
    bool KeepClothing,
    bool KeepHeld,
    bool HideOwnLayers,
    Vector2? Offset,
    Angle? Rotation,
    Color? Color,
    int? DrawDepth,
    bool? NoRotation);
