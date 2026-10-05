using Content.Shared._Starlight.Laspi;
using Content.Shared._Starlight.MagicMirror;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.MagicMirror;

namespace Content.Server._Starlight.Laspi;

/// <summary>
///  System that handles the InnateHairChangeComponent, which allows an entity to change their hair/facial hair using the magic mirror UI.
/// This is primarily used only on the Laspi and neo-Laspi species, but there's nothing stopping you from adding it to other species if you wanna be a hair wizard or something. :3
/// </summary>
public sealed class InnateHairChangeSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<InnateHairChangeComponent, InnateHairChangeActionEvent>(OnHairAction);
    }

    /// <summary>
    /// Handles the innate hair change action event, opens the magic mirror UI for you to change your hair/facial hair. Some of this code is duplicated from SharedMagicMirrorSystem.UpdateInterface() because I don't want to touch that file in this PR at all.
    /// </summary>
    private void OnHairAction(Entity<InnateHairChangeComponent> ent, ref InnateHairChangeActionEvent args)
    {
        // NOTE: Handled silently which may not be desired.
        if (args.Handled)
            return;

        if (!TryComp(ent.Owner, out HumanoidAppearanceComponent? humanoid))
            return;

        if (!TryComp(ent.Owner, out MagicMirrorComponent? mirror))
            return;

        var hair = humanoid.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hairMarkings) ? new List<Marking>(hairMarkings) : new();

        var facialHair = humanoid.MarkingSet.TryGetCategory(MarkingCategories.FacialHair, out var facialMarkings) ? new List<Marking>(facialMarkings) : new();

        var state = new MagicMirrorUiState(
            humanoid.Species,
            hair,
            humanoid.MarkingSet.PointsLeft(MarkingCategories.Hair) + hair.Count,
            facialHair,
            humanoid.MarkingSet.PointsLeft(MarkingCategories.FacialHair) + facialHair.Count);

        mirror.Target = ent.Owner;
        Dirty(ent.Owner, mirror); // Dirty, filthy entity. B)

        _ui.SetUiState(ent.Owner, MagicMirrorUiKey.Key, state);
        _ui.TryOpenUi(ent.Owner, MagicMirrorUiKey.Key, ent.Owner);

        args.Handled = true; // Action handled, don't let other systems mess with my perfect UI. >:(
    }
}
