using Content.Server.Ghost.Roles.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
#region Starlight
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Random;
#endregion

namespace Content.Server.Ghost.Roles;

/// <summary>
/// This handles logic and interaction related to <see cref="ToggleableGhostRoleComponent"/>
/// </summary>
public sealed class ToggleableGhostRoleSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    #region Starlight
    [Dependency] private IRobustRandom _random = default!;
    #endregion

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ToggleableGhostRoleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ToggleableGhostRoleComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ToggleableGhostRoleComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ToggleableGhostRoleComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<ToggleableGhostRoleComponent, GetVerbsEvent<ActivationVerb>>(AddWipeVerb);
        #region Starlight
        SubscribeLocalEvent<StationEventComponent, GameRuleStartedEvent>(OnStationEventStarted);
        #endregion
    }

    private void OnUseInHand(EntityUid uid, ToggleableGhostRoleComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // check if a mind is present
        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
        {
            _popup.PopupEntity(Loc.GetString(component.ExamineTextMindPresent), uid, args.User, PopupType.Large);
            return;
        }
        if (HasComp<GhostTakeoverAvailableComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString(component.ExamineTextMindSearching), uid, args.User);
            return;
        }

        // Starlight Start
        if (!TrySetSearching((uid, component), true))
            return;
        // Starlight End

        _popup.PopupEntity(Loc.GetString(component.BeginSearchingText), uid, args.User);

        // Starlight edit Start: Moved
        // UpdateAppearance(uid, ToggleableGhostRoleStatus.Searching);

        // ActivateGhostRole((uid, component));
        // Starlight edit End
    }

    public void ActivateGhostRole(Entity<ToggleableGhostRoleComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var ghostRole = EnsureComp<GhostRoleComponent>(ent);
        EnsureComp<GhostTakeoverAvailableComponent>(ent);

        // GhostRoleComponent inherits custom settings from the ToggleableGhostRoleComponent
        ghostRole.RoleName = Loc.GetString(ent.Comp.RoleName);
        ghostRole.RoleDescription = Loc.GetString(ent.Comp.RoleDescription);
        ghostRole.RoleRules = Loc.GetString(ent.Comp.RoleRules);
        ghostRole.JobProto = ent.Comp.JobProto;
        ghostRole.MindRoles = ent.Comp.MindRoles;
    }

    private void OnExamined(EntityUid uid, ToggleableGhostRoleComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
        {
            args.PushMarkup(Loc.GetString(component.ExamineTextMindPresent));
        }
        else if (HasComp<GhostTakeoverAvailableComponent>(uid))
        {
            args.PushMarkup(Loc.GetString(component.ExamineTextMindSearching));
        }
        else
        {
            args.PushMarkup(Loc.GetString(component.ExamineTextNoMind));
        }
    }

    private void OnMindAdded(EntityUid uid, ToggleableGhostRoleComponent pai, MindAddedMessage args)
    {
        // Mind was added, shutdown the ghost role stuff so it won't get in the way
        RemCompDeferred<GhostTakeoverAvailableComponent>(uid);
        UpdateAppearance(uid, ToggleableGhostRoleStatus.On);
    }

    private void OnMindRemoved(EntityUid uid, ToggleableGhostRoleComponent component, MindRemovedMessage args)
    {
        // Mind was removed, prepare for re-toggle of the role
        RemCompDeferred<GhostRoleComponent>(uid);
        UpdateAppearance(uid, ToggleableGhostRoleStatus.Off);
    }

    private void UpdateAppearance(EntityUid uid, ToggleableGhostRoleStatus status)
    {
        _appearance.SetData(uid, ToggleableGhostRoleVisuals.Status, status);
    }

    private void AddWipeVerb(EntityUid uid, ToggleableGhostRoleComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract)
            return;

        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
        {
            ActivationVerb verb = new()
            {
                Text = Loc.GetString(component.WipeVerbText),
                Act = () =>
                {
                    if (!_mind.TryGetMind(uid, out var mindId, out var mind))
                        return;
                    // Wiping device :(
                    // The shutdown of the Mind should cause automatic reset of the pAI during OnMindRemoved
                    _mind.TransferTo(mindId, null, mind: mind);
                    _popup.PopupEntity(Loc.GetString(component.WipeVerbPopup), uid, args.User, PopupType.Large);
                }
            };
            args.Verbs.Add(verb);
        }
        else if (HasComp<GhostTakeoverAvailableComponent>(uid))
        {
            ActivationVerb verb = new()
            {
                Text = Loc.GetString(component.StopSearchVerbText),
                Act = () =>
                {
                    if (!TrySetSearching((uid, component), false)) // Starlight Edit: Changed to Helper
                        return;

                    // Starlight edit Start: Moved
                    // RemCompDeferred<GhostTakeoverAvailableComponent>(uid);
                    // RemCompDeferred<GhostRoleComponent>(uid);
                    // Starlight edit End
                    _popup.PopupEntity(Loc.GetString(component.StopSearchVerbPopup), uid, args.User);
                    // UpdateAppearance(uid, ToggleableGhostRoleStatus.Off); // Starlight Edit: Moved
                }
            };
            args.Verbs.Add(verb);
        }
    }

    /// <summary>
    /// If there is a player present, kicks it out.
    /// If not, prevents future ghosts taking it.
    /// No popups are made, but appearance is updated.
    /// </summary>
    public void Wipe(EntityUid uid)
    {
        if (TryComp<MindContainerComponent>(uid, out var mindContainer) &&
            mindContainer.HasMind &&
            _mind.TryGetMind(uid, out var mindId, out var mind))
        {
            _mind.TransferTo(mindId, null, mind: mind);
        }

        // Starlight edit Start
        if (TryComp<ToggleableGhostRoleComponent>(uid, out var component))
            TrySetSearching((uid, component), false, allowMind: true);
        // Starlight edit End
    }

    #region Starlight
    private void OnStationEventStarted(Entity<StationEventComponent> ent, ref GameRuleStartedEvent args)
    {
        var query = EntityQueryEnumerator<ToggleableGhostRoleComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var toggleable, out var xform))
        {
            if (!ShouldEventToggle((uid, toggleable, xform), args.RuleId, ent.Comp.TargetStation))
                continue;

            ApplyEventToggle((uid, toggleable));
        }
    }

    private bool ShouldEventToggle(
        Entity<ToggleableGhostRoleComponent, TransformComponent> ent,
        string eventId,
        EntityUid? targetStation)
        => ent.Comp1.ToggleOnEvents.Contains(eventId)
            && (targetStation == null
            || (CompOrNull<StationMemberComponent>(ent.Comp2.GridUid)?.Station) == targetStation)
            && _random.Prob(Math.Clamp(ent.Comp1.ToggleOnEventChance, 0f, 1f));

    private void ApplyEventToggle(Entity<ToggleableGhostRoleComponent> ent)
    {
        var mode = ent.Comp.ToggleOnEventMode;

        if (mode == ToggleableGhostRoleComponent.EventToggleMode.None)
            return;

        if (mode.HasFlag(ToggleableGhostRoleComponent.EventToggleMode.Deactivate)
            && HasComp<GhostTakeoverAvailableComponent>(ent.Owner))
        {
            TrySetSearching(ent, false);
            return;
        }

        if (mode.HasFlag(ToggleableGhostRoleComponent.EventToggleMode.Activate))
        {
            TrySetSearching(ent, true);
        }
    }

    private bool TrySetSearching(Entity<ToggleableGhostRoleComponent> ent, bool searching, bool allowMind = false)
    {
        var hasMind = TryComp<MindContainerComponent>(ent.Owner, out var mindContainer) && mindContainer.HasMind;

        if (searching)
        {
            if (hasMind)
                return false;

            if (HasComp<GhostTakeoverAvailableComponent>(ent.Owner))
                return false;

            if (HasComp<GhostRoleComponent>(ent.Owner))
                return false;

            UpdateAppearance(ent.Owner, ToggleableGhostRoleStatus.Searching);
            ActivateGhostRole((ent.Owner, ent.Comp));
            return true;
        }

        if (hasMind && !allowMind)
            return false;

        if (!HasComp<GhostTakeoverAvailableComponent>(ent.Owner)
            && !HasComp<GhostRoleComponent>(ent.Owner))
            return false;

        RemCompDeferred<GhostTakeoverAvailableComponent>(ent.Owner);
        RemCompDeferred<GhostRoleComponent>(ent.Owner);
        UpdateAppearance(ent.Owner, ToggleableGhostRoleStatus.Off);
        return true;
    }
    #endregion
}
