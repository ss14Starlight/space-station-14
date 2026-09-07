using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)] // Starlight
public sealed partial class FootprintComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<FootprintData> Prints = new();
}

[Serializable, NetSerializable]
public readonly record struct FootprintData(Vector2 Offset, Angle Rotation, Color Color, string State);
