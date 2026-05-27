using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Roles;

namespace Content.Server._Starlight.Roles;

// Starlight - adds pirate objectives when the pirate mind role is assigned
public sealed class PirateRoleSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;

    public override void Initialize()
    {
        base.Initialize();
        // RoleAddedEvent fires on the mind entity AFTER MindRoleComponent is fully set up,
        // so MindHasRole (called by RoleRequirementSystem) will succeed at this point.
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        // Only proceed if the mind now has a PirateRoleComponent role.
        if (!_roles.MindHasRole<PirateRoleComponent>((args.MindId, args.Mind), out _))
            return;

        _mind.TryAddObjective(args.MindId, args.Mind, "PirateSurviveObjective");
    }
}
