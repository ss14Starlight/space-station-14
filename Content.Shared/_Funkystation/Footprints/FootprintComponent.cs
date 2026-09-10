using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)] // Starlight
public sealed partial class FootprintComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<FootprintData> Prints = new();

    /// <summary>
    /// The shared RGB tint for every print on this tile. Individual print opacity is stored separately.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Color BaseColor = Color.White;
}

[Serializable, NetSerializable]
public readonly record struct FootprintData(Vector2 Offset, Angle Rotation, float Alpha, FootprintVisualState State);
