using Content.Shared.Hands.EntitySystems;
using Content.Server.Antag;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking;
using Content.Server._Starlight.Railroading;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed class BrighteyeRuleSystem : GameRuleSystem<BrighteyeRuleComponent>
{
    [Dependency] private readonly RailroadDarkTaskSystem _railroadDarkTaskSystem = default!;


    protected override void AppendRoundEndText(EntityUid uid,
        BrighteyeRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        args.AddLine(Loc.GetString("brighteye-thedark"));
        args.AddLine(Loc.GetString("brighteye-darktiles", ("darkCount", _railroadDarkTaskSystem.CheckDarkTilesOnStation())));
        args.AddLine("");
    }
}
