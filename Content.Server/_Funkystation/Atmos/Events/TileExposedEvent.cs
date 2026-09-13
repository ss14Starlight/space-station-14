namespace Content.Server._Funkystation.Atmos.Events
{
    [ByRefEvent]
    public readonly record struct TileExposedEvent(Vector2i Tile, float Temperature, float Volume, EntityUid? SparkSource);
}
