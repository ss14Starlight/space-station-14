using Content.Shared._Starlight.Silicons;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Mind;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components; // Starlight
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Stunnable;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes; // Starlight
using Content.Shared.Emag.Components;
using Content.Shared.Interaction;
using Content.Shared.Mindshield.Components;

namespace Content.Shared.Silicons.Laws;

/// <summary>
/// This handles getting and displaying the laws for silicons.
/// </summary>
public abstract partial class SharedSiliconLawSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!; // Starlight

    /// <inheritdoc/>
    public override void Initialize()
    {
        InitializeUpdater();
        SubscribeLocalEvent<EmagSiliconLawComponent, GotEmaggedEvent>(OnGotEmagged);
        SubscribeLocalEvent<SiliconLawProviderComponent, AfterInteractEvent>(OnAfterInteract); // Starlight
    }

    private void OnGotEmagged(EntityUid uid, EmagSiliconLawComponent component, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction, args.EmagComponent)) // Starlight edit | allow bypass if differing factions
            return;

        // Starlight start - no go if they're mindshielded
        if (HasComp<MindShieldComponent>(uid))
        {
            _popup.PopupClient(Loc.GetString("law-emag-mindshielded"), uid, args.UserUid);
            return;
        } // Starlight end

        // prevent self-emagging
        if (uid == args.UserUid)
        {
            _popup.PopupClient(Loc.GetString("law-emag-cannot-emag-self"), uid, args.UserUid);
            return;
        }

        if (component.RequireOpenPanel &&
            TryComp<WiresPanelComponent>(uid, out var panel) &&
            !panel.Open)
        {
            _popup.PopupClient(Loc.GetString("law-emag-require-panel"), uid, args.UserUid);
            return;
        }

        //#region Starlight
        if (args.EmagComponent != null)
        {
            if (args.EmagComponent.DestroyTransponder)
            {
                RemComp<BorgTransponderComponent>(uid);
            }

            if (args.EmagComponent.Components != null)
                EntityManager.AddComponents(uid, args.EmagComponent.Components);
        }
        //#endregion Starlight

        var ev = new SiliconEmaggedEvent(args.UserUid, args.EmagComponent); // Starlight
        RaiseLocalEvent(uid, ref ev);

        component.OwnerName = Name(args.UserUid);

        //Starlight begin
        if(args.EmagComponent is not null)
            NotifyLawsChanged(uid, args.EmagComponent.DoEmaggedSound ? args.EmagComponent.EmaggedSoundOverride ?? component.EmaggedSound : null);
        else
            NotifyLawsChanged(uid, component.EmaggedSound);
        //Starlight end
        if (_mind.TryGetMind(uid, out var mindId, out _))
            EnsureSubvertedSiliconRole(mindId);

        _stunSystem.TryUpdateParalyzeDuration(uid, component.StunTime);

        args.Handled = true;
    }

    public virtual void NotifyLawsChanged(EntityUid uid, SoundSpecifier? cue = null)
    {

    }

    protected virtual void EnsureSubvertedSiliconRole(EntityUid mindId)
    {
        if (TryComp<MindComponent>(mindId, out var mind))
        {
            var owner = mind.OwnedEntity;
            if (TryComp<ShowCrewIconsComponent>(owner, out var crewIconComp))
            {
                crewIconComp.UncertainCrewBorder = true;
                Dirty(owner.Value, crewIconComp);
            }
        }
    }

    protected virtual void RemoveSubvertedSiliconRole(EntityUid mindId)
    {
        if (TryComp<MindComponent>(mindId, out var mind))
        {
            var owner = mind.OwnedEntity;
            if (TryComp<ShowCrewIconsComponent>(owner, out var crewIconComp))
            {
                crewIconComp.UncertainCrewBorder = false;
                Dirty(owner.Value, crewIconComp);
            }
        }
    }

    #region Starlight

    private void OnAfterInteract(Entity<SiliconLawProviderComponent> ent, ref AfterInteractEvent args)
    {
        if (HasComp<EmagComponent>(ent)) return;
        if (args.Target is not { } target) return;
        if (!HasComp<SiliconLawBoundComponent>(args.Target)) return;
        if (!HasComp<SiliconLawProviderComponent>(args.Target)) return;
        if (!TryComp(args.Target, out LawboardResettableComponent? lrComp)) return;

        // can't change own laws
        if (args.Target == args.User)
        {
            _popup.PopupEntity(Loc.GetString("law-updater-cannot-change-self"), target, args.User);
            return;
        }

        if (TryComp<WiresPanelComponent>(args.Target, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("law-updater-require-panel"), target, args.User);
            return;
        }

        if (HasComp<MindShieldComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("law-updater-mindshielded"), target, args.User);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("law-updater-alert", ("name", Name(args.User))),
            target, target, PopupType.Large
        );

        _doAfter.TryStartDoAfter(
            new DoAfterArgs(
                EntityManager, args.User, lrComp.Delay,
                new UpdateLawsDoAfterEvent(), args.Target, target: args.Target, used: args.Used
            )
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BreakOnHandChange = true,
                BreakOnDropItem = true,
                MovementThreshold = 0.5f,
                CancelDuplicate = true
            }
        );
    }

    #endregion
}

[ByRefEvent]
public record struct SiliconEmaggedEvent(EntityUid user, EmagComponent? emagComp);
