using Content.Client.Construction;
using Content.Shared._Starlight.Construction.Components;
using Content.Shared.Interaction.Events;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Construction;

[UsedImplicitly]
public sealed partial class ConstructionPlacementActionSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = null!;

    [SubscribeLocalEvent]
    private void OnUseInHand(Entity<ConstructionPlacementActionComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var prototype = _prototypeManager.Index(ent.Comp.Construction);
        EntityManager.System<ConstructionSystem>().StartQuickConstructionPlacement(prototype);
    }
}
