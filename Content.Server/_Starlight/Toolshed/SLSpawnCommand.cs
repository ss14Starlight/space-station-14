using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Content.Server.Administration;
using Content.Shared._Starlight.Toolshed;
using Content.Shared.Administration;
using Robust.Shared.Console.Commands;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Toolshed;

/// <summary>
/// Mirror of <see cref="SpawnCommand"/> with additional functionality (y'know, to avoid an engine pull request).
/// Do not use the overrides unless you REALLY know what you're doing, especially on live servers.
/// </summary>
/// <remarks>
/// As a result of the changes, you can not use slspawn:at. Not that it mattered TOO much...
/// Kind of a pain to get coordinates through Toolshed anyway...
/// </remarks>
[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed partial class SLSpawnCommand : ToolshedCommand
{
    private SharedContainerSystem? sharedContainerSystem = null;

    #region Event

    private EntityUid RaiseEvent(string? prototype, EntityUid target, string overrides)
    {
        var ev = new SLSpawnToolshedEvent(prototype, EntityManager.GetNetEntity(target), overrides);
        EntityManager.EntityNetManager.SendSystemNetworkMessage(ev);
        EntityManager.EventBus.RaiseEvent(EventSource.Local, ev);
        return EntityManager.GetEntity(ev.ServerSpawnedEntity ?? NetEntity.Invalid);
    }

    #endregion

    #region spawn:on implementations

    [CommandImplementation("on")]
    public EntityUid SpawnOn([PipedArgument] EntityUid target, EntProtoId proto,
        [Optional] [DefaultParameterValue("")] string overrideYaml) => RaiseEvent(proto, target, overrideYaml);

    [CommandImplementation("on")]
    public IEnumerable<EntityUid> SpawnOn([PipedArgument] IEnumerable<EntityUid> target, EntProtoId proto,
        [Optional] [DefaultParameterValue("")] string overrideYaml) =>
        target.Select(x => SpawnOn(x, proto, overrideYaml));

    #endregion

    #region spawn:in implementations

    [CommandImplementation("in")]
    public EntityUid SpawnIn([PipedArgument] EntityUid target, string containerId, EntProtoId proto,
        [Optional] [DefaultParameterValue("")] string overrideYaml)
    {
        var spawned = SpawnOn(target, proto, overrideYaml);
        if (!TryComp<TransformComponent>(spawned, out var transformComponent) ||
            !TryComp<MetaDataComponent>(spawned, out var metaDataComp))
            return spawned;
        // The PhysicsComponent isn't required, so continue with or without it
        TryComp<PhysicsComponent>(spawned, out var physicsComponent);
        sharedContainerSystem ??= EntityManager.System<SharedContainerSystem>();
        var container = sharedContainerSystem.GetContainer(target, containerId);
        sharedContainerSystem.InsertOrDrop((spawned, transformComponent, metaDataComp, physicsComponent),
            container
        );
        return spawned;
    }

    [CommandImplementation("in")]
    public IEnumerable<EntityUid> SpawnIn([PipedArgument] IEnumerable<EntityUid> target, string containerId,
        EntProtoId proto, [Optional] [DefaultParameterValue("")] string overrideYaml) =>
        target.Select(x => SpawnIn(x, containerId, proto, overrideYaml));

    #endregion

    #region spawn:attached implementations

    [CommandImplementation("attached")]
    public EntityUid SpawnIn([PipedArgument] EntityUid target, EntProtoId proto,
        [Optional] [DefaultParameterValue("")] string overrideYaml) =>
        RaiseEvent(proto, target, overrideYaml);

    [CommandImplementation("attached")]
    public IEnumerable<EntityUid> SpawnIn([PipedArgument] IEnumerable<EntityUid> target, EntProtoId proto,
        [Optional] [DefaultParameterValue("")] string overrideYaml) =>
        target.Select(x => SpawnIn(x, proto, overrideYaml));

    #endregion
}
