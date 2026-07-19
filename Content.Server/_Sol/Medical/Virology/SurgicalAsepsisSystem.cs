using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Handles washing / sterilizing surgical tools and gloves.
/// </summary>
public sealed class SurgicalAsepsisSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

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
        SubscribeLocalEvent<SurgicalToolSterilityComponent, MeleeHitEvent>(OnToolMeleeHit);
        // Unarmed attacks raise MeleeHitEvent on the attacker; dirty worn gloves.
        SubscribeLocalEvent<MobStateComponent, MeleeHitEvent>(OnUnarmedMeleeHit);
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

    private void OnToolMeleeHit(Entity<SurgicalToolSterilityComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !HitLivingTarget(args))
            return;

        MarkSterilityLost(ent);
    }

    private void OnUnarmedMeleeHit(Entity<MobStateComponent> ent, ref MeleeHitEvent args)
    {
        // Only unarmed: weapon entity is the attacker themselves.
        if (!args.IsHit || args.Weapon != ent.Owner || !HitLivingTarget(args))
            return;

        if (_inventory.TryGetSlotEntity(ent, "gloves", out var gloves) &&
            TryComp<SurgicalToolSterilityComponent>(gloves.Value, out var gloveSterility))
            MarkSterilityLost((gloves.Value, gloveSterility));
    }

    private bool HitLivingTarget(MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;

            if (HasComp<MobStateComponent>(target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Marks a surgical tool/gloves as dirty (e.g. after surgery or attacking someone).
    /// </summary>
    public void MarkSterilityLost(Entity<SurgicalToolSterilityComponent> ent)
    {
        if (ent.Comp.State == SurgicalSterilityState.Dirty)
            return;

        ent.Comp.State = SurgicalSterilityState.Dirty;
        Dirty(ent);

        if (TryComp<SurfaceContaminationComponent>(ent, out var surface))
        {
            surface.IsDirty = true;
            Dirty(ent.Owner, surface);
        }
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
