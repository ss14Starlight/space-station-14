using System.Linq;
using Content.Server.Disposal.Unit;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight
{
    public sealed class AutoLoaderSystem : EntitySystem
    {
        [Dependency] private readonly DisposableSystem _disposableSystem = default!;
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
        [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        public void Cycle(EntityUid entity, Entity<AutoLoaderComponent> autoloader, BaseContainer autoloadercontainer, EntityUid CurrentTube)
        {
            var holder = Spawn(autoloader.Comp.HolderPrototypeId, _xformSystem.GetMapCoordinates(autoloader, xform: Transform(autoloader)));
            var holderComponent = Comp<DisposalHolderComponent>(holder);

            foreach (var item in autoloadercontainer.ContainedEntities.ToArray())
                if (entity != item)
                    _containerSystem.Insert(item, holderComponent.Container);

            if (_whitelistSystem.IsWhitelistPass(autoloader.Comp.Whitelist, entity))
                _containerSystem.Insert(entity, autoloadercontainer);
            else
                _containerSystem.Insert(entity, holderComponent.Container);

            _disposableSystem.EnterTube(holder, CurrentTube, holderComponent);
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
}

