using Content.Shared._NullLink;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Abstract.Conditions;

public sealed partial class DiscordRolesRequirement : BaseRequirement
{
    [Dependency] public readonly IPrototypeManager _protos = default!;
    [Dependency] public readonly ISharedNullLinkPlayerRolesReqManager _nulllinkPlayerRoles = default!;

    [DataField(required: true)]
    public ProtoId<RoleRequirementPrototype>? Requirement;

    public override string GetRequirementDescription()
    {
        base.GetRequirementDescription();

        if (!_protos.TryIndex(Requirement, out var roleReq))
            return "";

        var requirement = Loc.GetString(
                    "roles-req-any-role-required-fail",
                    ("discord", Loc.GetString(roleReq.Discord)),
                    ("roles", Loc.GetString(roleReq.RolesLoc)));
        return requirement;
    }

    public override bool Handle(ICommonSession user)
    {
        base.Handle(user);

        return Requirement is not null
            && _protos.TryIndex(Requirement, out var roleReq)
            && _nulllinkPlayerRoles.IsAnyRole(user, roleReq.Roles);
    }
}
