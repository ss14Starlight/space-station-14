using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Toolshed;

[Serializable, NetSerializable]
public sealed class SLSpawnToolshedEvent(string? prototype, NetEntity target, string overrides) : EntityEventArgs
{
    public readonly string? Prototype = prototype;
    public readonly NetEntity Target = target;
    public readonly string Overrides = overrides;
    public NetEntity? ServerSpawnedEntity;
}
