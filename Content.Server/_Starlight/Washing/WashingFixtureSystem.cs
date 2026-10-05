using Content.Shared._Starlight.Lube;
using Content.Shared._Starlight.Washing;
using Content.Shared.DoAfter;
using Content.Shared.Glue;
using Content.Shared.Lube;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Washing;

/// <summary>
/// System for using washing fixtures for self-cleaning.
/// </summary>
public sealed partial class WashingFixtureSystem : SharedWashingFixtureSystem
{
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedCreamPieSystem _creamPie = default!;
    [Dependency] private GlueSystem _glueSystem = default!;
    [Dependency] private SharedLubedSystem _lubedSystem = default!;

    /// <summary>
    /// Starts a DoAfter for the washing action.
    /// </summary>
    protected override void TryStartCleaning(Entity<WashingFixtureComponent> washingFixtureComp, EntityUid user, EntityUid target)
    {
        if (!TryComp<WashingFixtureComponent>(target, out var washingComp))
        {
            return;
        }

        bool hasDirtyHands = HasComp<LubedComponent>(user) || HasComp<GluedComponent>(user);
        bool hasDirtyFace = TryComp<CreamPiedComponent>(user, out var creamPiedComp) && creamPiedComp.CreamPied;

        if (hasDirtyHands || hasDirtyFace)
        {
            var doAfterArgs = new DoAfterArgs(EntityManager, user, washingComp.CleanDelay, new WashingDoAfterEvent(), eventTarget: washingFixtureComp, target: target, used: target)
            {
                NeedHand = true,
                BreakOnMove = true,
                DistanceThreshold = washingComp.CleanDistance,
            };

            if (_doAfterSystem.TryStartDoAfter(doAfterArgs))
            {
                _audioSystem.PlayPvs(washingComp.SoundStart, target);
                _popupSystem.PopupEntity(Loc.GetString("washing-cleaning", ("target", target)), user, user, PopupType.Small);
            }
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("washing-cleaning-cannot-clean"), user, user, PopupType.Small);
        }
    }

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
