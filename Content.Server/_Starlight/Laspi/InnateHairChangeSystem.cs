using Content.Shared._Starlight.Laspi;
using Content.Shared._Starlight.MagicMirror;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.MagicMirror;

namespace Content.Server._Starlight.Laspi;

public sealed partial class InnateHairChangeSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnHairAction(Entity<InnateHairChangeComponent> ent, ref InnateHairChangeActionEvent args)
    {
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
        Dirty(ent.Owner, mirror);

        _ui.SetUiState(ent.Owner, MagicMirrorUiKey.Key, state);
        _ui.TryOpenUi(ent.Owner, MagicMirrorUiKey.Key, ent.Owner);

        args.Handled = true;
    }
}
