using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Kitchen.EntitySystems;

/// <summary>
///     Shared base system for butchering entities using a sharp tool.
/// </summary>
public abstract partial class SharedSharpSystem : EntitySystem
{
    [Dependency] protected SharedDoAfterSystem DoAfterSystem = default!;
    [Dependency] protected SharedPopupSystem PopupSystem = default!;
    [Dependency] protected SharedContainerSystem ContainerSystem = default!;
    [Dependency] protected MobStateSystem MobStateSystem = default!;

    /// <summary>
    ///     Subscribes standard interaction and verb-gathering events.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(IngestionSystem)]);
        SubscribeLocalEvent<ButcherableComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    /// <summary>
    ///     Handles interaction when a sharp tool is used on a target.
    /// </summary>
    private void OnAfterInteract(EntityUid uid, SharpComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is null || !args.CanReach)
            return;

        // Check to see if we can do surgery first.
        if (HasComp<SurgeryToolComponent>(uid) && HasComp<SurgeryTargetComponent>(args.Target.Value))
            return;

        if (TryStartButcherDoafter(uid, args.Target.Value, args.User))
            args.Handled = true;
    }

    /// <summary>
    ///     Gets the delay duration required to butcher a target using a specific knife/tool.
    /// </summary>
    public float GetButcherDelay(EntityUid knife, EntityUid target, SharpComponent? sharp = null, ButcherableComponent? butcher = null)
    {
        sharp ??= EntityManager.GetComponent<SharpComponent>(knife);
        butcher ??= EntityManager.GetComponent<ButcherableComponent>(target);

        return sharp.ButcherDelayModifier * butcher.ButcherDelay;
    }

    /// <summary>
    ///     Validates the butchering requirements and attempts to initiate the do-after timer.
    /// </summary>
    public bool TryStartButcherDoafter(EntityUid knife, EntityUid target, EntityUid user)
    {
        if (!TryComp<ButcherableComponent>(target, out var butcher))
            return false;

        if (!TryComp<SharpComponent>(knife, out var sharp))
            return false;

        if (TryComp<MobStateComponent>(target, out var mobState) && !MobStateSystem.IsDead(target, mobState))
            return false;

        if (butcher.Type != ButcheringType.Knife && target != user)
        {
            PopupSystem.PopupPredicted(Loc.GetString("butcherable-different-tool", ("target", target)), knife, user);
            return false;
        }

        if (!sharp.Butchering.Add(target))
            return false;

        Dirty(knife, sharp);

        // if the user isn't the entity with the sharp component,
        // they will need to be holding something with their hands, so we set needHand to true
        // so that the doafter can be interrupted if they drop the item in their hands
        var needHand = user != knife;

        var delay = GetButcherDelay(knife, target, sharp, butcher);

        var doAfter =
            new DoAfterArgs(EntityManager, user, delay, new SharpDoAfterEvent(), knife, target: target, used: knife)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = needHand,
            };

        if (!DoAfterSystem.TryStartDoAfter(doAfter))
        {
            sharp.Butchering.Remove(target);
            Dirty(knife, sharp);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Adds the "Butcher" verb to the interaction menu of butcherable targets.
    /// </summary>
    private void OnGetInteractionVerbs(EntityUid uid, ButcherableComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (component.Type != ButcheringType.Knife || !args.CanAccess || !args.CanInteract)
            return;

        // if the user has no hands, don't show them the verb if they have no SharpComponent either
        if (!TryComp<SharpComponent>(args.User, out var userSharpComp) && args.Hands == null)
            return;

        var disabled = false;
        string? message = null;

        // if the held item doesn't have SharpComponent
        // and the user doesn't have SharpComponent
        // disable the verb
        if (!TryComp<SharpComponent>(args.Using, out var usingSharpComp) && userSharpComp == null)
        {
            disabled = true;
            message = Loc.GetString("butcherable-need-knife",
                ("target", uid));
        }
        else if (ContainerSystem.IsEntityInContainer(uid))
        {
            disabled = true;
            message = Loc.GetString("butcherable-not-in-container",
                ("target", uid));
        }
        else if (TryComp<MobStateComponent>(uid, out var state) && !MobStateSystem.IsDead(uid, state))
        {
            disabled = true;
            message = Loc.GetString("butcherable-mob-isnt-dead");
        }

        // set the object doing the butchering to the item in the user's hands or to the user themselves
        // if either has the SharpComponent
        EntityUid sharpObject = default;
        if (usingSharpComp != null)
            sharpObject = args.Using!.Value;
        else if (userSharpComp != null)
            sharpObject = args.User;

        InteractionVerb verb = new()
        {
            Act = () =>
            {
                if (!disabled)
                    TryStartButcherDoafter(sharpObject, args.Target, args.User);
            },
            Message = message,
            Disabled = disabled,
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/cutlery.svg.192dpi.png")),
            Text = Loc.GetString("butcherable-verb-name"),
        };

        args.Verbs.Add(verb);
    }
}
