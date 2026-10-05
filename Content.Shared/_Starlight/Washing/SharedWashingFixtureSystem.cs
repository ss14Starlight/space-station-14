using Content.Shared.Glue;
using Content.Shared.Lube;
using Content.Shared.Nutrition.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Washing;

/// <summary>
/// System for using washing fixtures for self-cleaning.
/// </summary>
public abstract partial class SharedWashingFixtureSystem : EntitySystem
{
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

    protected abstract void TryStartCleaning(Entity<WashingFixtureComponent> entity, EntityUid user, EntityUid target);
}
