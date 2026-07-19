using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Hands.EntitySystems;
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
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolSurgeryStepCompletedEvent>(OnStepCompleted);
        SubscribeLocalEvent<SurgeryTargetComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnStepCompleted(ref SolSurgeryStepCompletedEvent args)
    {
        foreach (var tool in args.Tools)
        {
            if (!TryComp<SurgicalToolSterilityComponent>(tool, out var sterility))
                continue;

            if (sterility.State == SurgicalSterilityState.Dirty)
            {
                _popup.PopupEntity(
                    Loc.GetString("sol-surgery-dirty-tool-warning"),
                    args.User,
                    args.User,
                    PopupType.MediumCaution);
                break;
            }
        }

        if (args.Failed)
        {
            _popup.PopupEntity(
                Loc.GetString("sol-surgery-failed-infection-risk"),
                args.User,
                args.User,
                PopupType.SmallCaution);
        }
    }

    private void OnGetVerbs(Entity<SurgeryTargetComponent> target, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("sol-surgery-inspect-asepsis-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => InspectAsepsis(user, target),
            Priority = 2,
        });
    }

    private void InspectAsepsis(EntityUid user, EntityUid patient)
    {
        var dirtyTools = CountNonSterileHeld(user);
        var masked = _inventory.TryGetSlotEntity(user, "mask", out var mask) &&
                     HasComp<SurgicalMaskProtectionComponent>(mask.Value);

        _popup.PopupEntity(
            Loc.GetString("sol-surgery-asepsis-status", ("dirty", dirtyTools), ("masked", masked)),
            patient,
            user);
    }

    private int CountNonSterileHeld(EntityUid user)
    {
        var dirtyTools = 0;
        foreach (var tool in _hands.EnumerateHeld(user))
        {
            if (TryComp<SurgicalToolSterilityComponent>(tool, out var sterility) &&
                sterility.State != SurgicalSterilityState.Sterile)
            {
                dirtyTools++;
            }
        }

        return dirtyTools;
    }
}
