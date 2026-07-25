using System.Linq;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight;

public sealed partial class AutoLoaderSystem : EntitySystem
{
    [Dependency] private SharedDisposalHolderSystem _holderSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
        => base.Initialize();

    public void Cycle(EntityUid entity, Entity<AutoLoaderComponent> autoloader, BaseContainer autoloadercontainer, EntityUid tube)
    {
        // process each entity already inside of the autoloader
        foreach(var item in autoloadercontainer.ContainedEntities.ToArray())
        {
            if (item == entity)
                continue;

            // remove from autoloader
            _containerSystem.Remove(item, autoloadercontainer);

            // make a holder - part of the new Disposals Refactor, a wrapper entity that 'holds' onto the item
            var holder = Spawn(autoloader.Comp.HolderPrototypeId, _xformSystem.GetMapCoordinates(autoloader));
            var holderEnt = new Entity<DisposalHolderComponent>(holder, Comp<DisposalHolderComponent>(holder));

            _holderSystem.AttachEntity(holderEnt, item);

            // send item into disposal system
            _holderSystem.TryEnterTube(holderEnt, (tube, Comp<DisposalTubeComponent>(tube)));
        }

        // handle incoming entity
        if(_whitelistSystem.IsWhitelistPass(autoloader.Comp.Whitelist, entity))
        {
            _containerSystem.Insert(entity, autoloadercontainer);
            return;
        }

        var newHolder = Spawn(autoloader.Comp.HolderPrototypeId,
            _xformSystem.GetMapCoordinates(autoloader));

        var holderComp = Comp<DisposalHolderComponent>(newHolder);
        var holderEntity = new Entity<DisposalHolderComponent>(newHolder, holderComp);

        _holderSystem.AttachEntity(holderEntity, entity);
        _holderSystem.TryEnterTube(holderEntity, (tube, Comp<DisposalTubeComponent>(tube)));
    }
}

[RegisterComponent]
public sealed partial class AutoLoaderComponent : Component
{
    [DataField(required: true)]
    public string Container;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntProtoId HolderPrototypeId = "DisposalHolder";
}

