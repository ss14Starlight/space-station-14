using System.IO;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Toolshed;

public sealed partial class SLSpawnToolshedSystem : EntitySystem
{
    [Dependency] private ISerializationManager _serialize = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<SLSpawnToolshedEvent>(OnSLSpawnToolshed);
    }

    private void OnSLSpawnToolshed(SLSpawnToolshedEvent ev)
    {
#pragma warning disable RA0045
        var ent = EntityManager.SpawnEntity(ev.Prototype, Transform(GetEntity(ev.Target)).Coordinates,
            ParseOverrideString(ev.Overrides));
#pragma warning restore RA0045
        if (_net.IsServer)
            ev.ServerSpawnedEntity = GetNetEntity(ent);
    }

    public ComponentRegistry? ParseOverrideString(string yamlString)
    {
        if (yamlString == string.Empty) return null;
        var yml = DataNodeParser.ParseYamlStream(new StringReader(yamlString));
        return !yml.TryFirstOrDefault(out var document)
            ? null
            : _serialize.Read<ComponentRegistry>(document.Root, notNullableOverride: true);
    }
}
