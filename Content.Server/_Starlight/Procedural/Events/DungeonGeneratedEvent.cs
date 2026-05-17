using Content.Shared.Procedural;

namespace Content.Server._Starlight.Procedural.Events;

public sealed class DungeonGeneratedEvent(DungeonConfig config, int seed) : EntityEventArgs
{
    public readonly DungeonConfig Config = config;
    public readonly int Seed = seed;
}
