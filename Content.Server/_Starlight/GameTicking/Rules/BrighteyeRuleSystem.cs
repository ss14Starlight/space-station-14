using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using System.Text;
using Content.Server._Starlight.Railroading.TaskSystems;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class BrighteyeRuleSystem : GameRuleSystem<BrighteyeRuleComponent>
{
    [Dependency] private RailroadDarkTaskSystem _railroadDarkTaskSystem = default!;

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
