using Content.Shared.Hands.EntitySystems;
using Content.Server.Antag;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking;
using Content.Server._Starlight.Railroading;
using Content.Server.Objectives;
using System.Text;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed class BrighteyeRuleSystem : GameRuleSystem<BrighteyeRuleComponent>
{
    [Dependency] private readonly RailroadDarkTaskSystem _railroadDarkTaskSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrighteyeRuleComponent, ObjectivesTextPrependEvent>(OnTextPrepend);
    }

    private void OnTextPrepend(EntityUid uid, BrighteyeRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        var sb = new StringBuilder();

        sb.AppendLine(Loc.GetString("brighteye-thedark"));
        sb.AppendLine(Loc.GetString("brighteye-darktiles", ("darkCount", _railroadDarkTaskSystem.CheckDarkTilesOnStation())));
        sb.AppendLine(Loc.GetString("brighteye-darkstation"));

        args.Text = sb.ToString();
    }
}
