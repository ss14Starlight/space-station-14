using Content.Shared._NullLink;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Preferences.Loadouts;

namespace Content.Shared._Starlight.Preferences.Loadouts.Effects;

/// <summary>
/// Takes a list of PlayerFlags and checks if the player has any of them.
/// </summary>
public sealed partial class RolesReqLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public ProtoId<RoleRequirementPrototype> Proto = default!;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session,
        IDependencyCollection collection, out FormattedMessage reason)
    {
        var requirement = IoCManager.Resolve<IPrototypeManager>().Index(Proto);
        var success = session is not null && IoCManager.Resolve<ISharedNullLinkPlayerRolesReqManager>()
            .IsAnyRole(session, requirement.Roles);

        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            success? "roles-req-any-role-required-pass" : "roles-req-any-role-required-fail",
            ("discord", Loc.GetString(requirement.Discord)),
            ("roles", Loc.GetString(requirement.RolesLoc))));

        return success;
    }
}
