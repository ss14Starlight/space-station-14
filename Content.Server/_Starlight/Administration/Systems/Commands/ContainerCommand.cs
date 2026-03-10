using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Containers;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class ContainerCommand : ToolshedCommand
{
    private SharedContainerSystem? _container;
    
    #region insert implementations

    [CommandImplementation("insertentity")]
    public EntityUid InsertEntity([PipedArgument] EntityUid target, string containerId, EntityUid uid)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        var container = _container.GetContainer(target, containerId);
        _container.InsertOrDrop(uid, container);
        return target;
    }
    
    [CommandImplementation("insertentity")]
    public ContainerRef InsertEntity([PipedArgument] ContainerRef container, EntityUid uid)
    {
        if (container.Container is null) return container;
        _container ??= EntityManager.System<SharedContainerSystem>();
        _container.InsertOrDrop(uid, container.Container);
        return container;
    }

    [CommandImplementation("insertentity")]
    public IEnumerable<EntityUid> InsertEntity([PipedArgument] IEnumerable<EntityUid> target, string containerId, EntityUid uid) =>
        target.Select(x => InsertEntity(x, containerId, uid));

    [CommandImplementation("insert")]
    public EntityUid Insert([PipedArgument] EntityUid uid, string containerId, EntityUid target) =>
        InsertEntity(target, containerId, uid);

    [CommandImplementation("insert")]
    public IEnumerable<EntityUid> Insert([PipedArgument] IEnumerable<EntityUid> uid, string containerId, EntityUid target) =>
        uid.Select(x => InsertEntity(target, containerId, x));

    #endregion
    
    #region create implementations

    [CommandImplementation("create")]
    public EntityUid Create([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        _container.MakeContainer<Container>(target, containerId);
        return target;
    }

    [CommandImplementation("create")]
    public IEnumerable<EntityUid> Create([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.Select(x => Create(x, containerId));
    
    [CommandImplementation("createslot")]
    public EntityUid CreateSlot([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        _container.MakeContainer<ContainerSlot>(target, containerId);
        return target;
    }

    [CommandImplementation("createslot")]
    public IEnumerable<EntityUid> CreateSlot([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.Select(x => CreateSlot(x, containerId));
    
    #endregion
    
    #region delete implementations
    
    [CommandImplementation("delete")]
    public EntityUid Delete([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        var container = _container.GetContainer(target, containerId);
        _container.ShutdownContainer(container);
        return target;
    }
    
    [CommandImplementation("delete")]
    public void Delete([PipedArgument] ContainerRef container)
    {
        if (container.Container is null) return;
        _container ??= EntityManager.System<SharedContainerSystem>();
        _container.ShutdownContainer(container.Container);
    }

    [CommandImplementation("delete")]
    public IEnumerable<EntityUid> Delete([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.Select(x => Delete(x, containerId));
    
    #endregion
    
    #region drop implementation

    [CommandImplementation("drop")]
    public EntityUid Drop([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        var container = _container.GetContainer(target, containerId);
        _container.EmptyContainer(container);
        return target;
    }
    
    [CommandImplementation("drop")]
    public ContainerRef Drop([PipedArgument] ContainerRef container)
    {
        if (container.Container is null) return container;
        _container ??= EntityManager.System<SharedContainerSystem>();
        _container.EmptyContainer(container.Container);
        return container;
    }
    
    [CommandImplementation("dropandget")]
    public IEnumerable<EntityUid> DropGetEntities([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        var container = _container.GetContainer(target, containerId);
        return _container.EmptyContainer(container);
    }

    [CommandImplementation("dropandget")]
    public IEnumerable<EntityUid>? DropGetEntities([PipedArgument] ContainerRef container)
    {
        if (container.Container is null) return null;
        _container ??= EntityManager.System<SharedContainerSystem>();
        return _container.EmptyContainer(container.Container);
    }

    [CommandImplementation("dropanddelete")]
    public EntityUid DropAndDelete([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        var container = _container.GetContainer(target, containerId);
        _container.ShutdownContainer(container);
        return target;
    }
    
    [CommandImplementation("dropanddelete")]
    public void DropAndDelete([PipedArgument] ContainerRef container)
    {
        if (container.Container is null) return;
        _container ??= EntityManager.System<SharedContainerSystem>();
        _container.ShutdownContainer(container.Container);
    }
    
    [CommandImplementation("drop")]
    public IEnumerable<EntityUid> Drop([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.Select(x => Drop(x, containerId));
    
    [CommandImplementation("dropandget")]
    public IEnumerable<EntityUid> DropGetEntities([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.SelectMany(x=>DropGetEntities(x, containerId));

    [CommandImplementation("dropanddelete")]
    public IEnumerable<EntityUid> DropAndDelete([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.Select(x => DropAndDelete(x, containerId));

    #endregion
    
    #region get implementations

    [CommandImplementation("get")]
    public ContainerRef Get([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        return new ContainerRef(_container.GetContainer(target, containerId));
    }

    [CommandImplementation("getentities")]
    public IEnumerable<EntityUid> GetEntities([PipedArgument] EntityUid target, string containerId)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        var container = _container.GetContainer(target, containerId);
        return container.ContainedEntities;
    }

    [CommandImplementation("getcontaining")]
    public IEnumerable<ContainerRef> GetContaining([PipedArgument] EntityUid target)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        List<ContainerRef> containers = [];
        containers.AddRange(_container.GetContainingContainers(target).Select(container => new ContainerRef(container)));
        return containers;
    }

    [CommandImplementation("getoutercontainer")]
    public ContainerRef GetOuterContainer([PipedArgument] EntityUid target)
    {
        _container ??= EntityManager.System<SharedContainerSystem>();
        return new ContainerRef(_container.GetContainingContainers(target).Last());
    }

    [CommandImplementation("getowner")]
    public EntityUid? GetOwner([PipedArgument] ContainerRef container) => container.Container?.Owner;

    [CommandImplementation("get")]
    public IEnumerable<ContainerRef> Get([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.Select(x=>Get(x, containerId));

    [CommandImplementation("getentities")]
    public IEnumerable<EntityUid> GetEntities([PipedArgument] IEnumerable<EntityUid> target, string containerId) =>
        target.SelectMany(x => GetEntities(x, containerId));

    [CommandImplementation("getoutercontainer")]
    public IEnumerable<ContainerRef> GetOuterContainer([PipedArgument] IEnumerable<EntityUid> target) =>
        target.Select(GetOuterContainer);

    [CommandImplementation("getowner")]
    public IEnumerable<EntityUid?> GetOwner([PipedArgument] IEnumerable<ContainerRef> container) =>
        container.Select(GetOwner);

    #endregion
}

public readonly record struct ContainerRef(BaseContainer? Container)
{
    public override string ToString() => $"ContainerRef:{Container?.Owner}/{Container?.ID}";
}