using Content.Shared._Starlight.Laspi;
using Content.Shared._Starlight.MagicMirror;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.MagicMirror;

namespace Content.Server._Starlight.Laspi;

public sealed class InnateHairChangeSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        // Subscribe to the action event so we can open the mirror UI when the action is used
        SubscribeLocalEvent<InnateHairChangeComponent, InnateHairChangeActionEvent>(OnHairAction);
    }

    private void OnHairAction(Entity<InnateHairChangeComponent> ent, ref InnateHairChangeActionEvent args)
    {
        // NOTE: Handled silently which may not be desired.
        if (args.Handled)
            return;

        if (!TryComp(ent.Owner, out HumanoidAppearanceComponent? humanoid))
            return;

        if (!TryComp(ent.Owner, out MagicMirrorComponent? mirror))
            return;

        // This is the body of SharedMagicMirrorSystem.UpdateInterface()
        // I partly duplicated it instead of calling the protected method. This violates DRY principles, but whatever. :c
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
