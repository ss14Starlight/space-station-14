using Content.Shared._Starlight.Lube;
using Content.Shared._Starlight.Washing;
using Content.Shared.Glue;
using Content.Shared.Lube;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;

namespace Content.Server._Starlight.Washing;

/// <inheritdoc/>
public sealed partial class WashingFixtureSystem : SharedWashingFixtureSystem
{
    [Dependency] private SharedCreamPieSystem _creamPie = default!;
    [Dependency] private GlueSystem _glueSystem = default!;
    [Dependency] private SharedLubedSystem _lubedSystem = default!;

    /// <summary>
    /// Handles cleaning the user entity.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnWashingDoAfter(Entity<WashingFixtureComponent> ent, ref WashingDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;
        if (!HasComp<WashingFixtureComponent>(args.Target))
            return;
        if (TryComp<CreamPiedComponent>(args.User, out var creamPiedComp))
            _creamPie.SetCreamPied(args.User, creamPiedComp, false);
        if (HasComp<LubedComponent>(args.User))
            _lubedSystem.RemoveLubed(args.User);
        if (HasComp<GluedComponent>(args.User))
            _glueSystem.RemoveGlued(args.User);

        _audioSystem.PlayPvs(ent.Comp.SoundCompleted, args.Target.Value);
        _popupSystem.PopupEntity(Loc.GetString("washing-cleaning-success"), args.User, args.User, PopupType.Medium);
        args.Handled = true;
    }
}
