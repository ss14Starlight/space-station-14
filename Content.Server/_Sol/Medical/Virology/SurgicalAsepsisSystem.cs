using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Handles washing / sterilizing surgical tools and gloves.
/// </summary>
public sealed class SurgicalAsepsisSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PathogenSystem _pathogen = default!;

    private static readonly HashSet<string> DisinfectantReagents = new(StringComparer.OrdinalIgnoreCase)
    {
        "SpaceCleaner",
        "Sterilizine",
        "SolSterilizine",
        "Ethanol",
    };

    private static readonly HashSet<string> SterilantReagents = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sterilizine",
        "SolSterilizine",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgicalToolSterilityComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SurgicalToolSterilityComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<SurgicalToolSterilityComponent, AfterInteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SurgicalToolSterilityComponent, ReactionEntityEvent>(OnReaction);
    }

    private void OnExamined(Entity<SurgicalToolSterilityComponent> ent, ref ExaminedEvent args)
    {
        var state = ent.Comp.State switch
        {
            SurgicalSterilityState.Sterile => Loc.GetString("sol-surgery-tool-sterile"),
            SurgicalSterilityState.Disinfected => Loc.GetString("sol-surgery-tool-disinfected"),
            SurgicalSterilityState.Dirty => Loc.GetString("sol-surgery-tool-dirty"),
            _ => string.Empty,
        };

        args.PushMarkup(state);

        // Pathogen wording is only meaningful on virology stations.
        if (ent.Comp.Contaminants.Count > 0 &&
            (_pathogen.IsVirologyEnabledAt(ent) || _pathogen.IsVirologyEnabledAt(args.Examiner)))
            args.PushMarkup(Loc.GetString("sol-surgery-tool-contaminated"));
    }

    private void OnGetVerbs(Entity<SurgicalToolSterilityComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("sol-surgery-tool-wash-verb"),
            Act = () => TryWash(ent, user, sterilize: false),
            Priority = 1,
        });
    }

    private void OnInteractUsing(Entity<SurgicalToolSterilityComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!_solutions.TryGetDrainableSolution(args.Used, out var solEnt, out var solution) &&
            !_solutions.TryGetMixableSolution(args.Used, out solEnt, out solution))
            return;

        if (solution == null)
            return;

        if (TryCleanWithSolution(ent, solution, args.User))
        {
            args.Handled = true;
            _solutions.UpdateChemicals(solEnt.Value);
        }
    }

    private void OnReaction(Entity<SurgicalToolSterilityComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch)
            return;

        ApplyReagentClean(ent, args.Reagent, null);
    }

    public bool TryWash(Entity<SurgicalToolSterilityComponent> ent, EntityUid user, bool sterilize)
    {
        if (sterilize)
        {
            ClearContamination(ent, SurgicalSterilityState.Sterile);
            _popup.PopupEntity(Loc.GetString("sol-surgery-tool-sterilized"), ent, user);
            return true;
        }

        ReduceLoad(ent, 0.5f);
        ent.Comp.State = ent.Comp.Contaminants.Count == 0
            ? SurgicalSterilityState.Disinfected
            : SurgicalSterilityState.Dirty;

        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("sol-surgery-tool-washed"), ent, user);
        return true;
    }

    public bool TryCleanWithSolution(Entity<SurgicalToolSterilityComponent> ent, Solution solution, EntityUid? user)
    {
        var hasSterilant = false;
        var hasDisinfectant = false;

        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (quantity <= 0)
                continue;

            if (SterilantReagents.Contains(reagent.Prototype))
                hasSterilant = true;
            else if (DisinfectantReagents.Contains(reagent.Prototype))
                hasDisinfectant = true;
        }

        if (!hasSterilant && !hasDisinfectant)
        {
            if (solution.Volume <= 0)
                return false;

            ReduceLoad(ent, 0.35f);
            ent.Comp.State = ent.Comp.Contaminants.Count == 0
                ? SurgicalSterilityState.Disinfected
                : SurgicalSterilityState.Dirty;
            Dirty(ent);
            if (user != null)
                _popup.PopupEntity(Loc.GetString("sol-surgery-tool-washed"), ent, user.Value);
            return true;
        }

        if (hasSterilant)
        {
            ClearContamination(ent, SurgicalSterilityState.Sterile);
            if (user != null)
                _popup.PopupEntity(Loc.GetString("sol-surgery-tool-sterilized"), ent, user.Value);
            return true;
        }

        ReduceLoad(ent, 0.75f);
        ent.Comp.State = ent.Comp.Contaminants.Count == 0
            ? SurgicalSterilityState.Disinfected
            : SurgicalSterilityState.Dirty;
        Dirty(ent);
        if (user != null)
            _popup.PopupEntity(Loc.GetString("sol-surgery-tool-disinfected-popup"), ent, user.Value);
        return true;
    }

    private void ApplyReagentClean(Entity<SurgicalToolSterilityComponent> ent, ReagentPrototype reagent, EntityUid? user)
    {
        if (SterilantReagents.Contains(reagent.ID))
        {
            ClearContamination(ent, SurgicalSterilityState.Sterile);
            return;
        }

        if (!DisinfectantReagents.Contains(reagent.ID))
            return;

        ReduceLoad(ent, 0.75f);
        ent.Comp.State = ent.Comp.Contaminants.Count == 0
            ? SurgicalSterilityState.Disinfected
            : SurgicalSterilityState.Dirty;
        Dirty(ent);
    }

    private void ReduceLoad(Entity<SurgicalToolSterilityComponent> ent, float fractionRemaining)
    {
        for (var i = ent.Comp.Contaminants.Count - 1; i >= 0; i--)
        {
            ent.Comp.Contaminants[i].Load *= fractionRemaining;
            if (ent.Comp.Contaminants[i].Load < 0.05f)
                ent.Comp.Contaminants.RemoveAt(i);
        }

        if (TryComp<SurfaceContaminationComponent>(ent, out var surface))
        {
            for (var i = surface.Contaminants.Count - 1; i >= 0; i--)
            {
                surface.Contaminants[i].Load *= fractionRemaining;
                if (surface.Contaminants[i].Load < 0.05f)
                    surface.Contaminants.RemoveAt(i);
            }

            if (surface.Contaminants.Count == 0)
                surface.IsDirty = ent.Comp.State != SurgicalSterilityState.Sterile;

            Dirty(ent.Owner, surface);
        }
    }

    private void ClearContamination(Entity<SurgicalToolSterilityComponent> ent, SurgicalSterilityState state)
    {
        ent.Comp.Contaminants.Clear();
        ent.Comp.State = state;
        Dirty(ent);

        if (TryComp<SurfaceContaminationComponent>(ent, out var surface))
        {
            surface.Contaminants.Clear();
            surface.IsDirty = state != SurgicalSterilityState.Sterile;
            Dirty(ent.Owner, surface);
        }
    }
}
