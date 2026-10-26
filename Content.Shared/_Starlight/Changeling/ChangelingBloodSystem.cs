using System.Linq;
using Content.Shared._Blimpuf.Chemistry.Reagent;
using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Changeling;

/// <summary>
/// Disguises external blood samples and assimilates transfusions compatible with the current form.
/// </summary>
public sealed partial class ChangelingBloodSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<ReagentPrototype> ChangelingBlood = "BloodChangeling";
    private static readonly ProtoId<ReagentPrototype> OrdinaryBlood = "Blood";
    private static readonly ProtoId<ReagentPrototype> Slime = "Slime";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodstreamComponent, ExternalSolutionSampleEvent>(OnExternalSample);
        SubscribeLocalEvent<BloodstreamComponent, SolutionChangedEvent>(OnSolutionChanged);
    }

    private void OnExternalSample(Entity<BloodstreamComponent> donor, ref ExternalSolutionSampleEvent args)
    {
        var sample = args.Sample;
        if (!sample.ContainsPrototype(ChangelingBlood))
            return;

        var normalBlood = GetNormalBlood(donor.Owner, out var appearance);

        for (var i = sample.Contents.Count - 1; i >= 0; i--)
        {
            var reagent = sample.Contents[i];
            if (reagent.Reagent.Prototype != ChangelingBlood)
                continue;

            var data = reagent.Reagent.Data?.Select(d => d.Clone()).ToList() ?? [];
            data.RemoveAll(d => d is ReagentColorData);
            var color = donor.Comp.BloodReagentColor;
            if (color == null && normalBlood.ContainsPrototype(Slime) && appearance != null)
                color = appearance.SkinColor;
            if (color != null)
                data.Add(new ReagentColorData { Color = color.Value });

            var replacement = normalBlood.Clone();
            replacement.ScaleTo(reagent.Quantity);
            sample.RemoveReagent(reagent);
            foreach (var part in replacement.Contents)
                sample.AddReagent(new ReagentId(part.Reagent.Prototype, data), part.Quantity);
        }
    }

    private void OnSolutionChanged(Entity<BloodstreamComponent> recipient, ref SolutionChangedEvent args)
    {
        var reference = recipient.Comp.BloodReferenceSolution;
        if (!reference.ContainsPrototype(ChangelingBlood) ||
            args.Solution.Comp.Id != recipient.Comp.BloodSolutionName)
            return;

        var normalBlood = GetNormalBlood(recipient.Owner, out _);
        var blood = args.Solution.Comp.Solution;
        for (var i = blood.Contents.Count - 1; i >= 0; i--)
        {
            var reagent = blood.Contents[i];
            if (!normalBlood.ContainsPrototype(reagent.Reagent.Prototype))
                continue;

            var data = reagent.Reagent.Data?.Select(d => d.Clone()).ToList() ?? [];
            data.RemoveAll(d => d is ReagentColorData);
            blood.RemoveReagent(reagent);
            blood.AddReagent(new ReagentId(ChangelingBlood, data), reagent.Quantity);
        }

        // UpdateChemicals dirties the solution after this event. Do not call it recursively:
        // only compatible blood changes identity; volume, DNA, and other reagents are retained.
    }

    private Solution GetNormalBlood(EntityUid donor, out HumanoidAppearanceComponent? appearance)
    {
        var prototype = MetaData(donor).EntityPrototype;
        TryComp(donor, out appearance);
        if (appearance != null && _prototypes.Resolve(appearance.Species, out var species)
            && _prototypes.Resolve(species.Prototype, out var speciesPrototype))
            prototype = speciesPrototype;

        var normalBlood = prototype != null && prototype.TryGetComponent<BloodstreamComponent>(out var body, Factory)
            ? body.BloodReferenceSolution
            : new Solution(OrdinaryBlood, 1);
        return normalBlood.Volume == 0 || normalBlood.ContainsPrototype(ChangelingBlood)
            ? new Solution(OrdinaryBlood, 1)
            : normalBlood;
    }
}
