using Content.Shared.NullLink.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._NullLink;

public abstract partial class SharedPlayerRolesReqManager : ISharedNullLinkPlayerRolesReqManager
{
    [Dependency] protected IPrototypeManager _proto = default!;
    [Dependency] protected IConfigurationManager _cfg = default!;

    public void Initialize()
    {
        _cfg.OnValueChanged(NullLinkCCVars.RoleReqWithAccessToAllRoles, UpdateAllRoles, true);
        _cfg.OnValueChanged(NullLinkCCVars.RoleReqMentors, UpdateMentors, true);
        _cfg.OnValueChanged(NullLinkCCVars.RoleReqPeacefulBypass, UpdateRoleReqPeacefulBypass, true);
    }

    private void UpdateAllRoles(string obj)
    {
        if (_proto.TryIndex<RoleRequirementPrototype>(obj, out var allRoles))
            AllRoles = allRoles;
    }

    private void UpdateMentors(string obj)
    {
        if (!_proto.TryIndex<RoleRequirementPrototype>(obj, out var mentorReq))
            return;
        _mentorReq = mentorReq;
    }

    private void UpdateRoleReqPeacefulBypass(string obj)
    {
        if (!_proto.TryIndex<RoleRequirementPrototype>(obj, out var peacefulBypass))
            return;
        _peacefulBypass = peacefulBypass;
    }

    // --- ---

    protected RoleRequirementPrototype? _peacefulBypass;
    public abstract bool IsPeacefulBypass(EntityUid uid);

    // --- ---

    protected RoleRequirementPrototype? _mentorReq;
    public abstract bool IsMentor(EntityUid uid);
    public abstract bool IsMentor(ICommonSession session);

    // --- ---

    protected RoleRequirementPrototype? AllRoles;
    public abstract bool IsAllRolesAvailable(EntityUid uid);

    public abstract bool IsAllRolesAvailable(ICommonSession session);

    // --- ---

    public abstract bool IsAnyRole(ICommonSession session, ulong[] roles);
}
