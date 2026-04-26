using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Starlight.Roles.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Roles;

/// <summary>
/// Ensures the wizard mob entity has the Wizard tag whenever a WizardRoleComponent or
/// WizardDuelistRoleComponent mind role is assigned, and removes it when both are gone.
/// This guarantees that RestrictByUserTag checks on wizard items always work correctly,
/// regardless of the spawn path (roundstart, midround ghost role, admin force-make-antag, etc.).
/// </summary>
public sealed class WizardRoleSystem : EntitySystem
{
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WizardTag = "Wizard";

    public override void Initialize()
    {
        base.Initialize();
        // Primary path: role is added after the mind already owns an entity.
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        // Fallback path: the mind is transferred into a body after the role was already added
        // (e.g. ghost-role takeover where RoleAddedEvent fires before OwnedEntity is set).
        SubscribeLocalEvent<MindAddedMessage>(OnMindAdded);
        // Removal path: strip the tag when the wizard role is removed, if no wizard role remains.
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
    }

    private bool IsWizardMind(EntityUid mindId, MindComponent mind)
    {
        return _roles.MindHasRole<WizardRoleComponent>(mindId)
            || _roles.MindHasRole<WizardDuelistRoleComponent>(mindId);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (!IsWizardMind(args.MindId, args.Mind))
            return;

        var ownedEntity = args.Mind.OwnedEntity;
        if (ownedEntity == null)
            return;

        _tag.AddTag(ownedEntity.Value, WizardTag);
    }

    /// <summary>
    /// Fallback: fires on the mob entity when a mind is transferred into it.
    /// Covers cases where RoleAddedEvent fired before OwnedEntity was assigned.
    /// </summary>
    private void OnMindAdded(MindAddedMessage args)
    {
        if (!IsWizardMind(args.Mind.Owner, args.Mind.Comp))
            return;

        _tag.AddTag(args.Container.Owner, WizardTag);
    }

    /// <summary>
    /// Fires after a role is removed from the mind. Only strip the tag if
    /// no wizard role of any kind remains on this mind.
    /// </summary>
    private void OnRoleRemoved(RoleRemovedEvent args)
    {
        if (IsWizardMind(args.MindId, args.Mind))
            return;

        var ownedEntity = args.Mind.OwnedEntity;
        if (ownedEntity == null)
            return;

        _tag.RemoveTag(ownedEntity.Value, WizardTag);
    }
}
