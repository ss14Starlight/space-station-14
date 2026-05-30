using Content.Shared._NullLink;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Abstract.Conditions;

public sealed partial class NullLinkPlayTimeRequirement : BaseRequirement
{
    [Dependency] public readonly IPrototypeManager _protos = default!;
    [Dependency] public readonly INullLinkPlayTimeManager _playtime = default!;

    [DataField(required: true)]
    public string Server = "";

    [DataField(required: true)]
    public string Tracker = "";

    [DataField(required: true)]
    public TimeSpan Time = TimeSpan.Zero;

    public override string GetRequirementDescription()
    {
        base.GetRequirementDescription();

        var playtime = _playtime.GetPlayTime(Server, Guid.Empty, Tracker);

        var requirement = Loc.GetString(
                    "requirements-playtime",
                    ("server", Loc.GetString(Server.ToLower().Replace(".","-"))),
                    ("tracker", Loc.GetString(Tracker.ToLower())),
                    ("time", (Time - playtime).ToString(@"hh\:mm\:ss")));
        return requirement;
    }

    public override bool Handle(ICommonSession user)
    {
        base.Handle(user);

        return _playtime.GetPlayTime(Server, user.UserId, Tracker) >= Time;
    }
}
