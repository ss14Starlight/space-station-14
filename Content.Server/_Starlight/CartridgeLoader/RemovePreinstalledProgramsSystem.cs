using System.Linq;
using Content.Shared._Starlight.CartridgeLoader;
using Content.Shared.CartridgeLoader;

namespace Content.Server._Starlight.CartridgeLoader;

/// <inheritdoc cref="RemovePreinstalledProgramsComponent"/>
public sealed partial class RemovePreinstalledProgramsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RemovePreinstalledProgramsComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<RemovePreinstalledProgramsComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<CartridgeLoaderComponent>(ent, out var loader))
            return;

        loader.PreinstalledPrograms = loader.PreinstalledPrograms
            .Where(program => !ent.Comp.Programs.Contains(program))
            .ToList();
    }
}
