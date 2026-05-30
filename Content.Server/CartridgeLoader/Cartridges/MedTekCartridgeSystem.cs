using Content.Server.Medical.Components;
using Content.Shared.CartridgeLoader;
//FarHorizons Start
using Content.Shared.Verbs;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Interaction;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared._FarHorizons.CartridgeLoader.Cartridges;
//FarHorizons End

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed class MedTekCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!; //FarHorizons
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedTekCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<MedTekCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
        //FarHorizons Start
        SubscribeLocalEvent<HealthAnalyzerComponent, MedTekActionEvent>(OnMedTekAction);
        SubscribeLocalEvent<HealthAnalyzerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HealthAnalyzerComponent, InventoryRelayedEvent<GetVerbsEvent<InnateVerb>>>(AddVerbAnalyzer);
        //FarHorizons End
    }

    private void OnCartridgeAdded(Entity<MedTekCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        var healthAnalyzer = EnsureComp<HealthAnalyzerComponent>(args.Loader);
    }

    private void OnCartridgeRemoved(Entity<MedTekCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        // only remove when the program itself is removed
        if (!_cartridgeLoaderSystem.HasProgram<MedTekCartridgeComponent>(args.Loader))
        {
            RemComp<HealthAnalyzerComponent>(args.Loader);
        }
    }

    //FarHorizons Start
    private void AddVerbAnalyzer(Entity<HealthAnalyzerComponent> ent, ref InventoryRelayedEvent<GetVerbsEvent<InnateVerb>> args)
    {
        if (!args.Args.CanInteract || !args.Args.CanAccess)
            return;
        if (!HasComp<DamageableComponent>(args.Args.Target) || !HasComp<MobStateComponent>(args.Args.Target))
            return;

        var user = args.Args.User;
        var target = args.Args.Target;

        if (TryComp(target, out TransformComponent? targetTransform))
        {
            var patientCoordinates = targetTransform.Coordinates;
            InnateVerb verb = new()
            {
                Act = () => _interactionSystem.InteractDoAfter(user, ent.Owner, target, patientCoordinates, true), // Setting canReach to true, because if it's false - args.Args.CanAccess will be false and this code won't run
                Text = "Analyze Patient",
                IconEntity = GetNetEntity(ent),
                Priority = 2,
            };
            args.Args.Verbs.Add(verb);
        }
    }

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
