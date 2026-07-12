using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;

namespace Content.Shared._Starlight.CartridgeLoader.Cartridges;

/// <summary>
/// Adds the predicted Analyze Patient verb for PDAs with the MedTek cartridge.
/// </summary>
public sealed class MedTekAnalyzerSystem : EntitySystem
{
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedTekAnalyzerComponent, InventoryRelayedEvent<GetVerbsEvent<InnateVerb>>>(AddVerbAnalyzer);
    }

    private void AddVerbAnalyzer(Entity<MedTekAnalyzerComponent> ent, ref InventoryRelayedEvent<GetVerbsEvent<InnateVerb>> args)
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
                Act = () => _interactionSystem.InteractDoAfter(user, ent.Owner, target, patientCoordinates, true),
                Text = Loc.GetString("med-tek-analyze-verb-name"),
                IconEntity = GetNetEntity(ent),
                Priority = 2,
            };
            args.Args.Verbs.Add(verb);
        }
    }
}
