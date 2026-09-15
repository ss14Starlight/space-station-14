using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Shared helpers for the tutorial Starlight surgery Bound UI.
/// </summary>
public abstract partial class SharedTutorialStarlightSurgerySystem : EntitySystem
{
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedHandsSystem Hands = default!;
    [Dependency] protected StandingStateSystem Standing = default!;

    public bool IsLyingDown(EntityUid entity) => Standing.IsDown(entity);

    public bool TryFindHeldTool(
        EntityUid surgeon,
        TutorialStarlightSurgeryToolType toolType,
        out EntityUid tool)
    {
        foreach (var held in Hands.EnumerateHeld(surgeon))
        {
            if (!TryComp<TutorialStarlightSurgeryToolComponent>(held, out var toolComp))
                continue;

            if (toolComp.ToolType != toolType)
                continue;

            if (toolType == TutorialStarlightSurgeryToolType.EyeImplant &&
                !HasComp<TutorialStarlightEyeImplantComponent>(held))
                continue;

            tool = held;
            return true;
        }

        tool = default;
        return false;
    }

    public bool IsStepComplete(
        TutorialStarlightSurgeryTargetComponent target,
        string surgeryId,
        string stepId)
        => target.CompletedSteps.Contains($"{surgeryId}:{stepId}");

    public bool AreRequirementsMet(
        TutorialStarlightSurgeryTargetComponent target,
        TutorialStarlightSurgeryPrototype surgery)
    {
        foreach (var req in surgery.Requirements)
        {
            if (!target.CompletedSurgeries.Contains(req.Id))
                return false;
        }

        return true;
    }

    public int? GetNextStepIndex(
        TutorialStarlightSurgeryTargetComponent target,
        TutorialStarlightSurgeryPrototype surgery)
    {
        for (var i = 0; i < surgery.Steps.Count; i++)
        {
            if (!IsStepComplete(target, surgery.ID, surgery.Steps[i].Id))
                return i;
        }

        return null;
    }

    public bool IsSurgeryAvailable(
        TutorialStarlightSurgeryTargetComponent target,
        TutorialStarlightSurgeryPrototype surgery)
    {
        if (surgery.RequiresNoEyeImplant && target.HasEyeImplant)
            return false;

        if (!AreRequirementsMet(target, surgery) &&
            !target.StartedSurgeries.Contains(surgery.ID) &&
            !target.CompletedSurgeries.Contains(surgery.ID))
            return false;

        return true;
    }
}
