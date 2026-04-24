using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Roles;

/// <summary>
/// Ensures the wizard mob entity has the Wizard tag whenever a WizardRoleComponent mind role is assigned.
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
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (!_roles.MindHasRole<WizardRoleComponent>((args.MindId, args.Mind), out _))
            return;

        var ownedEntity = args.Mind.OwnedEntity;
        if (ownedEntity == null)
            return;

        _tag.AddTag(ownedEntity.Value, WizardTag);
    }

    // Fallback: fires on the mob entity when a mind is transferred into it.
    // Covers cases where RoleAddedEvent fired before OwnedEntity was assigned.
    private void OnMindAdded(MindAddedMessage args)
    {
        Entity<MindComponent?> mind = new(args.Mind.Owner, args.Mind.Comp);
        if (!_roles.MindHasRole<WizardRoleComponent>(mind, out _))
            return;

        _tag.AddTag(args.Container.Owner, WizardTag);
    }
}
