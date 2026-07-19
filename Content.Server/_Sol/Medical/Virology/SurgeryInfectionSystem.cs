using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Calculates and applies surgery-related pathogen infection risk on virology stations.
/// </summary>
public sealed class SurgeryInfectionSystem : EntitySystem
{
    public const float DefaultBaseChance = 0.12f;
    public const string DefaultSurgeryPathogen = "SolPathogenWoundSepsis";

    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    /// Deterministic infection roll override for tests (0-1). Null uses RNG.
    /// </summary>
    public float? ForcedRoll { get; set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolSurgeryStepCompletedEvent>(OnSurgeryStepCompleted);
    }

    private void OnSurgeryStepCompleted(ref SolSurgeryStepCompletedEvent args)
    {
        var modifiers = CalculateModifiers(args.User, args.Body, args.Tools, args.Failed);
        if (!modifiers.StationEnabled)
            return;

        // Gloves and tools lose sterility after every applicable surgery step.
        foreach (var tool in args.Tools)
            MarkSterilityLost(tool);

        if (_inventory.TryGetSlotEntity(args.User, "gloves", out var gloves))
            MarkSterilityLost(gloves.Value);

        if (modifiers.FinalChance <= 0f || modifiers.SelectedPathogenId == null)
            return;

        var roll = ForcedRoll ?? _random.NextFloat();
        if (roll > modifiers.FinalChance)
            return;

        _pathogen.TryExpose(
            args.Body,
            modifiers.SelectedPathogenId,
            dose: 1.5f,
            PathogenTransmission.Surgery,
            source: args.User,
            force: true);

        // Contaminate used tools from the successful infection exposure.
        foreach (var tool in args.Tools)
            MarkToolUsed(tool, modifiers.SelectedPathogenId, args.Body);

        if (gloves != null)
            MarkToolUsed(gloves.Value, modifiers.SelectedPathogenId, args.Body);
    }

    public SurgeryInfectionModifiers CalculateModifiers(
        EntityUid user,
        EntityUid body,
        List<EntityUid> tools,
        bool failed)
    {
        var modifiers = new SurgeryInfectionModifiers
        {
            BaseChance = DefaultBaseChance,
            StationEnabled = _pathogen.IsVirologyEnabledAt(body),
        };

        if (!modifiers.StationEnabled)
        {
            modifiers.FinalChance = 0f;
            return modifiers;
        }

        if (!_pathogen.TryGetVirologyStation(body, out _, out var station) || !station.AllowSurgeryInfection)
        {
            modifiers.StationEnabled = false;
            modifiers.FinalChance = 0f;
            return modifiers;
        }

        modifiers.SelectedPathogenId = SelectPathogen(user, body, tools);

        // Operator carriage
        if (_pathogen.GetInfection(user, modifiers.SelectedPathogenId) is { Stage: not PathogenStage.Recovering })
            modifiers.OperatorCarrierMultiplier = 2.5f;

        // Surgical mask
        if (_inventory.TryGetSlotEntity(user, "mask", out var mask) &&
            HasComp<SurgicalMaskProtectionComponent>(mask.Value))
        {
            modifiers.MaskMultiplier = Comp<SurgicalMaskProtectionComponent>(mask.Value).OperatorDropletMultiplier;
        }
        else
        {
            modifiers.MaskMultiplier = 1.4f; // unmasked penalty
        }

        // Tools
        var toolMult = 1f;
        var anyTool = false;
        foreach (var tool in tools)
        {
            if (!TryComp<SurgicalToolSterilityComponent>(tool, out var sterility))
                continue;

            anyTool = true;
            toolMult = Math.Max(toolMult, sterility.State switch
            {
                SurgicalSterilityState.Sterile => 1f,
                SurgicalSterilityState.Disinfected => 1.35f,
                SurgicalSterilityState.Dirty => sterility.DirtyInfectionMultiplier,
                _ => 1f,
            });

            foreach (var contaminant in sterility.Contaminants)
            {
                if (contaminant.Load > 0.1f)
                    toolMult = Math.Max(toolMult, 2f + contaminant.Load * 0.1f);
            }
        }

        modifiers.ToolMultiplier = anyTool ? toolMult : 1.75f; // bare improvised tools

        // Gloves
        if (_inventory.TryGetSlotEntity(user, "gloves", out var gloves))
        {
            if (TryComp<SurgicalToolSterilityComponent>(gloves.Value, out var gloveSterility) &&
                gloveSterility.State == SurgicalSterilityState.Dirty)
                modifiers.GloveMultiplier = 1.8f;
            else if (TryComp<SurfaceContaminationComponent>(gloves.Value, out var gloveContam) && gloveContam.IsDirty)
                modifiers.GloveMultiplier = 1.6f;
            else
                modifiers.GloveMultiplier = 1f;
        }
        else
        {
            modifiers.GloveMultiplier = 2.2f; // bare hands
        }

        // Environment
        var envLoad = _pathogen.GetTotalContamination(body);
        if (TryComp<AirborneContaminantComponent>(body, out var airborne))
        {
            foreach (var entry in airborne.Contaminants)
                envLoad += entry.Load;
        }

        modifiers.EnvironmentMultiplier = envLoad > 0.5f ? 1f + Math.Min(envLoad, 5f) * 0.15f : 1f;

        // Open wound / incision assumed during active surgery steps
        modifiers.WoundMultiplier = 1.25f;
        modifiers.FailureMultiplier = failed ? 1.75f : 1f;

        if (modifiers.SelectedPathogenId != null &&
            _pathogen.TryResolvePathogen(modifiers.SelectedPathogenId, out var pathogen) &&
            pathogen != null)
            modifiers.ImmunityMultiplier = _pathogen.GetImmunityMultiplier(body, pathogen);

        modifiers.Recalculate();
        return modifiers;
    }

    private string SelectPathogen(EntityUid user, EntityUid body, List<EntityUid> tools)
    {
        // Prefer pathogens present on dirty tools or the operator.
        foreach (var tool in tools)
        {
            if (!TryComp<SurgicalToolSterilityComponent>(tool, out var sterility))
                continue;

            foreach (var entry in sterility.Contaminants)
            {
                if (entry.Load > 0 && _pathogen.TryResolvePathogen(entry.PathogenId, out _))
                    return entry.PathogenId;
            }
        }

        if (TryComp<PathogenCarrierComponent>(user, out var carrier))
        {
            foreach (var infection in carrier.Infections)
            {
                if (_pathogen.TryResolvePathogen(infection.PathogenId, out _))
                    return infection.PathogenId;
            }
        }

        return DefaultSurgeryPathogen;
    }

    private void MarkSterilityLost(EntityUid tool)
    {
        var sterility = EnsureComp<SurgicalToolSterilityComponent>(tool);
        if (sterility.State == SurgicalSterilityState.Dirty)
            return;

        sterility.State = SurgicalSterilityState.Dirty;
        Dirty(tool, sterility);

        if (TryComp<SurfaceContaminationComponent>(tool, out var surface))
        {
            surface.IsDirty = true;
            Dirty(tool, surface);
        }
    }

    private void MarkToolUsed(EntityUid tool, string pathogenId, EntityUid patient)
    {
        MarkSterilityLost(tool);
        var sterility = EnsureComp<SurgicalToolSterilityComponent>(tool);

        var found = false;
        foreach (var entry in sterility.Contaminants)
        {
            if (entry.PathogenId != pathogenId)
                continue;
            entry.Load += 1f;
            found = true;
            break;
        }

        if (!found)
        {
            sterility.Contaminants.Add(new PathogenContaminationEntry
            {
                PathogenId = pathogenId,
                Load = 1f,
            });
        }

        Dirty(tool, sterility);
        _pathogen.AddOrIncreaseContamination(tool, pathogenId, 1f);

        // Also pick up patient pathogens.
        if (TryComp<PathogenCarrierComponent>(patient, out var carrier))
        {
            foreach (var infection in carrier.Infections)
                _pathogen.AddOrIncreaseContamination(tool, infection.PathogenId, 0.5f);
        }
    }
}
