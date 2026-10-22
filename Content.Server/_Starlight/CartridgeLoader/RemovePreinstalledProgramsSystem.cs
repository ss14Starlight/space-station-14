using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Shared._Starlight.CartridgeLoader;
using Content.Shared.CartridgeLoader;

namespace Content.Server._Starlight.CartridgeLoader;

/// <inheritdoc cref="RemovePreinstalledProgramsComponent"/>
public sealed partial class RemovePreinstalledProgramsSystem : EntitySystem
{
    [SubscribeLocalEvent(before: [typeof(CartridgeLoaderSystem)])]
    private void OnMapInit(Entity<RemovePreinstalledProgramsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<CartridgeLoaderComponent>(ent, out var loader))
            return;

        loader.PreinstalledPrograms = loader.PreinstalledPrograms
            .Where(program => !ent.Comp.Programs.Contains(program))
            .ToList();
    }
}
