using System.Linq;
using Content.Server.Fax;
using Content.Server.GameTicking.Rules;
using Content.Shared._Starlight.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Fax;

public sealed partial class SendFaxRuleSystem : GameRuleSystem<SendFaxRuleComponent>
{
    [Dependency] private FaxSystem _fax = null!;
    [Dependency] private IGameTiming _timing = null!;

    protected override void Started(EntityUid uid, SendFaxRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (!component.EnqueueFaxes)
        {
            foreach (var fax in component.Faxes)
                SendFax(uid, fax);
            ForceEndSelf(uid, gameRule);
            return;
        }

        foreach (var fax in component.Faxes)
            component.QueuedFaxes.Enqueue(fax);

        var firstFax = component.QueuedFaxes.First();
        if (firstFax.Delay is null) return;
        component.NextFaxTime = _timing.CurTime + TimeSpan.FromSeconds(firstFax.Delay.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var faxRule, out var gameRule))
        {
            if (!faxRule.EnqueueFaxes) continue;
            if (_timing.CurTime < faxRule.NextFaxTime) continue;
            var data = faxRule.QueuedFaxes.Dequeue();
            SendFax(uid, data);
            if (faxRule.QueuedFaxes.Count == 0)
            {
                ForceEndSelf(uid, gameRule);
                continue;
            }

            var next = faxRule.QueuedFaxes.First();
            if (next.Delay is null) continue;
            faxRule.NextFaxTime = _timing.CurTime + TimeSpan.FromSeconds(next.Delay.Value);
        }
    }

    private void SendFax(EntityUid ruleUid, SendFaxRuleData data)
    {
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!data.Addresses.Contains(comp.FaxName))
                continue;
            if (data.FaxPrototype is not null)
                _fax.SendFaxWithData(uid, data.FaxPrototype.Value, data.PaperData);
            else if (data.PaperData is not null)
                _fax.SendFaxWithData(uid, data.PaperData);
            else Log.Warning($"Rule {ToPrettyString(ruleUid)} tried to send fax with no valid data.");
        }
    }
}
