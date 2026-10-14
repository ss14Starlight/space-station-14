using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

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
    /// Visual disguises shown to this client while hysteria vision is active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<HysteriaDisguiseSprite> DisguiseSprites = new();
}

/// <summary>
/// Defines a disguise sprite for hysteria vision.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct HysteriaDisguiseSprite(string Path, string State, Vector2 Size);
