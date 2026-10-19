// ReSharper disable CheckNamespace
using Robust.Shared.Map;

namespace Content.Server.Pinpointer;

public sealed partial class NavMapSystem
{
    private const string SecretFloorId = "FloorSecret";

    private bool IsSecretFloor(Tile tile) =>
        !tile.IsEmpty && _turfSystem.GetContentTileDefinition(tile).ID == SecretFloorId;

    private int SecretFloorWallMask(Tile tile) => IsSecretFloor(tile) ? WallMask : 0;
}
