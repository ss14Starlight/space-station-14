using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
/// Component added to entities that are experiencing hysteria vision.
/// They will see other humanoids as !random monsters
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HysteriaVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>
    /// The vampire who applied this effect
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Source;

    /// <summary>
    /// Defines a disguise sprite for hysteria vision
    /// </summary>
    [DataRecord]
    public partial record struct HysteriaDisguiseSprite(string Path, string State, Vector2 Size);
    public static readonly HysteriaDisguiseSprite[] DisguiseSprites =
    {
        new("/_Starlight/Vampire/Effects.rsi", "schizo", new Vector2(1.5f, 1.5f)),
        new("/Mobs/Animals/bear.rsi", "bear", new Vector2(1.65f, 1.65f)),
    };
}
