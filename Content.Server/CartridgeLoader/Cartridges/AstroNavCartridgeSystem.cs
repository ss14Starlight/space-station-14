using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.GPS.Components;
using Content.Shared._Starlight.Astronav;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed partial class AstroNavCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoaderSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AstroNavCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<AstroNavCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
    }

    private void OnCartridgeAdded(Entity<AstroNavCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        EnsureComp<HandheldGPSComponent>(args.Loader);
        EnsureComp<AstroNavComponent>(args.Loader); // Starlight-edit
    }

    private void OnCartridgeRemoved(Entity<AstroNavCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        // only remove when the program itself is removed
        if (!_cartridgeLoaderSystem.HasProgram<AstroNavCartridgeComponent>(args.Loader))
        {
            RemComp<HandheldGPSComponent>(args.Loader);
            RemComp<AstroNavComponent>(args.Loader); // Starlight-edit
        }
    }
}
