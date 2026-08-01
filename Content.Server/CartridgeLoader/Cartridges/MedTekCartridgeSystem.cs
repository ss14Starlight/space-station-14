using Content.Server.Medical.Components;
using Content.Shared.CartridgeLoader;
//FarHorizons Start
using Content.Shared.Interaction;
using Content.Shared.Actions;
using Content.Shared._Starlight.CartridgeLoader.Cartridges;
//FarHorizons End

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed partial class MedTekCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!; //FarHorizons
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedTekCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<MedTekCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
        //FarHorizons Start
        SubscribeLocalEvent<HealthAnalyzerComponent, MedTekActionEvent>(OnMedTekAction);
        SubscribeLocalEvent<HealthAnalyzerComponent, GetItemActionsEvent>(OnGetActions);
        //FarHorizons End
    }

    private void OnCartridgeAdded(Entity<MedTekCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        var healthAnalyzer = EnsureComp<HealthAnalyzerComponent>(args.Loader);
        EnsureComp<MedTekAnalyzerComponent>(args.Loader); // Starlight
    }

    private void OnCartridgeRemoved(Entity<MedTekCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        // only remove when the program itself is removed
        if (!_cartridgeLoaderSystem.HasProgram<MedTekCartridgeComponent>(args.Loader))
        {
            RemComp<HealthAnalyzerComponent>(args.Loader);
            RemComp<MedTekAnalyzerComponent>(args.Loader); // Starlight
        }
    }

    //FarHorizons Start
    private void OnGetActions(Entity<HealthAnalyzerComponent> ent, ref GetItemActionsEvent args)
    {
        if (_cartridgeLoaderSystem.HasProgram<MedTekCartridgeComponent>(ent.Owner))
        {
            args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
        }
    }

    private void OnMedTekAction(Entity<HealthAnalyzerComponent> ent, ref MedTekActionEvent args)
    {
        var user = args.Performer;
        var target = args.Target;
        if (TryComp(target, out TransformComponent? targetTransform))
        {
            var patientCoordinates = targetTransform.Coordinates;
            _interactionSystem.InteractDoAfter(user, ent.Owner, target, patientCoordinates, true);
        }
    }
    //FarHorizons End
}
