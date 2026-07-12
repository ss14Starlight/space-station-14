using Content.Server._Starlight.Lube;
using Content.Server.DoAfter;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Starlight.Washing;
using Content.Shared.DoAfter;
using Content.Shared.Glue;
using Content.Shared.Lube;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Washing;

/// <summary>
/// System for using washing fixtures for self-cleaning.
/// </summary>
public sealed partial class WashingFixtureSystem : EntitySystem
{
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private DoAfterSystem _doAfterSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private CreamPieSystem _creamPie = default!;
    [Dependency] private GlueSystem _glueSystem = default!;
    [Dependency] private LubedSystem _lubedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WashingFixtureComponent, GetVerbsEvent<AlternativeVerb>>(OnVerb);
        SubscribeLocalEvent<WashingFixtureComponent, WashingDoAfterEvent>(OnWashingDoAfter);
    }

    /// <summary>
    /// Checks if the interacting user can wash, and adds the interaction verb if they do.
    /// </summary>
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
