using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Mind;
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
        // RoleAddedEvent fires on the mind entity AFTER MindRoleComponent is fully set up,
        // and AFTER TransferTo has moved the player into their new body.
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
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
}
