using System.Linq;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Abstract.Conditions;

public sealed partial class UsernameRequirement : BaseRequirement
{
    [DataField(required: true)]
    public string Username = "";

    public override bool Handle(ICommonSession user)
    {
        base.Handle(user);

        return user.Name == Username || user.Name.Split('@').LastOrDefault() == Username;
    }
}
