using Content.Server._Sol.Medical.Virology;
using Content.Server.Examine;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._Sol.Medical.Surgery;

/// <summary>
/// Contextual surgery helpers: dirty-tool warnings, sterility-aware prompts, and part-focused cues.
/// The client BUI is <c>SolContextualSurgeryBui</c>; this system covers server-side feedback.
/// </summary>
public sealed class SolContextualSurgerySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolSurgeryStepCompletedEvent>(OnStepCompleted);
        SubscribeLocalEvent<SurgeryTargetComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnStepCompleted(ref SolSurgeryStepCompletedEvent args)
    {
        // Infection-framed feedback only applies where surgery infection is active.
        if (!_pathogen.IsVirologyEnabledAt(args.Body) && !_pathogen.IsVirologyEnabledAt(args.User))
            return;

        foreach (var tool in args.Tools)
        {
            if (!TryComp<SurgicalToolSterilityComponent>(tool, out var sterility))
                continue;

            // Only fully dirty tools elevate infection risk.
            if (sterility.State != SurgicalSterilityState.Dirty)
                continue;

            _popup.PopupEntity(Loc.GetString("sol-surgery-dirty-tool-warning"), args.User, args.User, PopupType.MediumCaution);
            break;
        }

        if (args.Failed)
            _popup.PopupEntity(Loc.GetString("sol-surgery-failed-infection-risk"), args.User, args.User, PopupType.SmallCaution);
    }

    private void OnGetVerbs(Entity<SurgeryTargetComponent> target, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Hygiene / infection context is only meaningful on virology stations.
        if (!_pathogen.IsVirologyEnabledAt(target) && !_pathogen.IsVirologyEnabledAt(args.User))
            return;

        var user = args.User;
        var patient = target.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("sol-surgery-inspect-hygiene-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => InspectHygiene(user, patient),
            Priority = 2,
        });
    }

    private void InspectHygiene(EntityUid user, EntityUid patient)
    {
        var bodyState = "clean";
        if (TryComp<SurfaceContaminationComponent>(patient, out var surface))
        {
            if (surface.Contaminants.Count > 0)
                bodyState = "contaminated";
            else if (surface.IsDirty)
                bodyState = "dirty";
        }

        var gloveState = "none";
        if (_inventory.TryGetSlotEntity(patient, "gloves", out var gloves))
        {
            if (TryComp<SurgicalToolSterilityComponent>(gloves.Value, out var gloveSterility))
            {
                gloveState = gloveSterility.State switch
                {
                    SurgicalSterilityState.Sterile => "sterile",
                    SurgicalSterilityState.Disinfected => "disinfected",
                    _ => "dirty",
                };
            }
            else if (TryComp<SurfaceContaminationComponent>(gloves.Value, out var gloveSurface) &&
                     (gloveSurface.IsDirty || gloveSurface.Contaminants.Count > 0))
            {
                gloveState = "dirty";
            }
            else
            {
                gloveState = "clean";
            }
        }

        var masked = _inventory.TryGetSlotEntity(patient, "mask", out var mask) &&
                     HasComp<SurgicalMaskProtectionComponent>(mask.Value);

        // Popups do not render RichText; use an examine tooltip so [color] tags work.
        var message = FormattedMessage.FromMarkupOrThrow(Loc.GetString(
            "sol-surgery-hygiene-status",
            ("body", bodyState),
            ("gloves", gloveState),
            ("masked", masked)));
        _examine.SendExamineTooltip(user, patient, message, getVerbs: false, centerAtCursor: false);
    }
}
