using Content.Shared.DoAfter;
using Content.Shared.Glue;
using Content.Shared.Lube;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Washing;

/// <summary>
/// System for using washing fixtures for self-cleaning.
/// </summary>
public abstract partial class SharedWashingFixtureSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem _audioSystem = default!;
    [Dependency] protected SharedPopupSystem _popupSystem = default!;
    [Dependency] protected SharedDoAfterSystem _doAfterSystem = default!;

    /// <summary>
    /// Checks if the interacting user can wash, and adds the interaction verb if they do.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnVerb(Entity<WashingFixtureComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !args.CanComplexInteract)
            return;

        // These need to be set outside for the anonymous method!
        var user = args.User;
        var target = args.Target;

        bool isDirty = (TryComp<CreamPiedComponent>(user, out var creamPiedComp) && creamPiedComp.CreamPied)
            || HasComp<LubedComponent>(user)
            || HasComp<GluedComponent>(user);

        var verb = new AlternativeVerb()
        {
            Act = () => TryStartCleaning(entity, user, target),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
            Text = Loc.GetString("washing-verb-text"),
            Message = Loc.GetString(isDirty ? "washing-verb-message" : "washing-verb-message-disabled"),
            Disabled = !isDirty,
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Starts a DoAfter for the washing action.
    /// </summary>
    private void TryStartCleaning(Entity<WashingFixtureComponent> washingFixtureComp, EntityUid user, EntityUid target)
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
                _audioSystem.PlayPredicted(washingComp.SoundStart, target, user);
                _popupSystem.PopupClient(Loc.GetString("washing-cleaning", ("target", target)), user, user, PopupType.Small);
            }
        }
        else
        {
            _popupSystem.PopupClient(Loc.GetString("washing-cleaning-cannot-clean"), user, user, PopupType.Small);
        }
    }
}
