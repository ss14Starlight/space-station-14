// ReSharper disable CheckNamespace
using Content.Server.Chemistry.Components;
using Content.Shared._Starlight.Chemistry;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.Chemistry.EntitySystems;

public sealed partial class TransformableContainerSystem
{
    private ReagentPrototype? GetDisplayedReagent(Entity<TransformableContainerComponent> entity)
    {
        if (TryComp<ChameleonGlassComponent>(entity.Owner, out var chameleon) &&
            !string.IsNullOrEmpty(chameleon.SelectedReagent) &&
            _prototypeManager.TryIndex<ReagentPrototype>(chameleon.SelectedReagent, out var selectedReagent))
            return selectedReagent;

        return _prototypeManager.Resolve(entity.Comp.CurrentReagent, out var currentReagent)
            ? currentReagent
            : null;
    }

    /// <summary>
    /// Keep the examine description in sync with the drink appearance, while leaving the solution untouched.
    /// </summary>
    public void RefreshDisplayedDescription(EntityUid uid)
    {
        if (!TryComp<TransformableContainerComponent>(uid, out var component))
            return;

        var displayedReagent = GetDisplayedReagent((uid, component));
        if (displayedReagent != null)
            _metadataSystem.SetEntityDescription(uid, displayedReagent.LocalizedDescription);
        else if (!string.IsNullOrEmpty(component.InitialDescription))
            _metadataSystem.SetEntityDescription(uid, component.InitialDescription);
    }
}
