using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Abstract.Conditions;

public sealed partial class UserIdRequirement : BaseRequirement
{
    [DataField(required: true)]
    public NetUserId UserId;

    public override bool Handle(ICommonSession user)
    {
        base.Handle(user);

        return user.UserId == UserId;
    }
}
