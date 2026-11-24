using Content.Shared._Starlight.UserInterface.Setup;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;

// ReSharper disable CheckNamespace
namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    [SubscribeLocalEvent]
    private void OnWiresGetAlternativeVerb(Entity<WiresPanelComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanComplexInteract
            || !HasComp<StationAiHeldComponent>(args.User)
            || !args.CanInteract
            || !TryComp<StationAiWhitelistComponent>(ent.Owner, out var whitelist)
            || !whitelist.Enabled
            || (_uiSystem.HasUi(ent.Owner, AiUi.Key)
                && !HasComp<SurveillanceCameraComponent>(ent.Owner)
                && !HasComp<SetupableComponent>(ent.Owner))
            || !_uiSystem.HasUi(ent.Owner, WiresUiKey.Key)
            || (TryComp<WiresPanelSecurityComponent>(ent.Owner, out var panelSecurity)
                && !panelSecurity.WiresAccessible)
            || !CanAccessGrid((args.User, null), Transform(ent.Owner).GridUid)
            || _vision.IsOutsideCameraViewCached(ent.Owner))
            return;

        var target = ent.Owner;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("station-ai-access-wires"),
            Priority = int.MinValue / 2,
            DoContactInteraction = false,
            Act = () => RaiseLocalEvent(target, (object) new StationAiOpenWiresPanelEvent { User = user })
        });
    }
}
