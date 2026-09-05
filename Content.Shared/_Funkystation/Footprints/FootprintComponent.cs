using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FootprintComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<FootprintData> Prints = new();

    [DataField]
    public ResPath Sprites = new("/Textures/_Funkystation/Effects/footprints.rsi");
}

[Serializable, NetSerializable]
public readonly record struct FootprintData(Vector2 Offset, Angle Rotation, Color Color, string State);
