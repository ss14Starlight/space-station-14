using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects;

/// <summary>
/// Default metabolism for drink reagents. Attempts to find a ThirstComponent on the target,
/// and to update it's thirst values.
/// </summary>
public sealed partial class SelfReplication : EntityEffect
{
    /// <summary>
    /// what is the maximum ammount this can "grow" effect tick
    /// </summary>
    [DataField]
    public FixedPoint2 MaxAmmount = 0.5f;

    /// <summary>
    /// will also try to pull chems from this container if this container is empty (or cant hold anymore)
    /// </summary>
    [DataField]
    public string BackupContainer = "bloodstream";

    public override bool ShouldLog => true; //I gotta see if it is happening and this is a NASTY effect

    /// Satiate thirst if a ThirstComponent can be found
    public override void Effect(EntityEffectBaseArgs args)
    {
        var uid = args.TargetEntity;
        if (args is not EntityEffectReagentArgs eera)
            return; //we dont self-replicate in botany trays and the like.
        var reagent = eera.Reagent;
        if (reagent == null)
            return; //what do you mean we are trying to grey goo without a reagent?
        var source = eera.Source;
        if (source == null)
            return; //what do you mean we are trying to grey goo. without a container to store outselves in?
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var remainingToCanabalize = MaxAmmount * eera.Scale; //it may be a issue to scale it but eh thats a problem for later me :>
        var amntToAdd = new FixedPoint2();
        foreach (var (consumable, amount) in source.GetReagentPrototypes(protoMan))
        {
            if (consumable == reagent)
                continue; //we dont wanna try and consume outselves to add more.
            var consumed = FixedPoint2.Min(remainingToCanabalize, amount);
            var amnt = source.RemoveReagent(new ReagentQuantity(consumable.ID, consumed));
            remainingToCanabalize -= amnt;
            amntToAdd += amnt;
            if (remainingToCanabalize == 0)
                break; //we reached the max ammount to consume. so we leave now.
        }

        if (remainingToCanabalize > 0 &&
            args.EntityManager.TryGetComponent<SolutionContainerManagerComponent>(uid, out var solnManComp) &&
            args.EntityManager.TrySystem<SharedSolutionContainerSystem>(out var solnSystem) &&
            solnSystem.TryGetSolution((uid, solnManComp), BackupContainer, out _, out var container) &&
            container != null
            )
        {
            foreach (var (consumable, amount) in container.GetReagentPrototypes(protoMan))
            {
                if (consumable == reagent)
                    continue;
                var consumed = FixedPoint2.Min(remainingToCanabalize, amount);
                var amnt = container.RemoveReagent(new ReagentQuantity(consumable.ID, consumed), ignoreReagentData: true);
                remainingToCanabalize -= amnt;
                amntToAdd += amnt;
                if (remainingToCanabalize == 0)
                    break;
            }
        }

        source.AddReagent(reagent.ID, amntToAdd);
        if (amntToAdd != 0) //Refund the chem used in the reaction if it reacted at all.
            source.AddReagent(reagent.ID, eera.Quantity);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-self-replicates", ("ammount", MaxAmmount), ("backup",  BackupContainer));
}
