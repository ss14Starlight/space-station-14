using Content.Server.Chat.Systems;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.Language.Components;
using Content.Shared.Alert;
using Content.Shared.Popups;
using Robust.Shared.GameStates;

namespace Content.Server._Starlight.BodyControl;

public sealed partial class BodyControlSystem
{
    [Dependency] private SharedPopupSystem _controlPopup = default!;

    private void Deny(EntityUid actor, string reason)
    {
        if (!TryComp<BodyControlSourceComponent>(actor, out var source) || _controlTiming.CurTime < source.NextDeniedPopup)
            return;
        source.NextDeniedPopup = _controlTiming.CurTime + TimeSpan.FromMilliseconds(600);
        _controlPopup.PopupEntity(Loc.GetString("body-control-denied-" + reason), source.Body, actor);
    }

    [SubscribeLocalEvent]
    private void OnAlertsVisibility(Entity<AlertsComponent> body, ref ComponentGetStateAttemptEvent args)
        => CheckControlVisibility(body, ref args);

    [SubscribeLocalEvent]
    private void OnLanguageVisibility(Entity<LanguageSpeakerComponent> body, ref ComponentGetStateAttemptEvent args)
        => CheckControlVisibility(body, ref args);

    private void CheckControlVisibility(EntityUid body, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player == null || args.Player.AttachedEntity == body)
            return;
        args.Cancelled |= args.Player.AttachedEntity is not { } actor
                          || !TryGetControl(actor, out var controlled, out _) || controlled != body;
    }

    [SubscribeLocalEvent]
    private void OnDenied(Entity<BodyControlSourceComponent> ent, ref BodyControlDeniedEvent args)
        => Deny(ent, args.Reason);

    [SubscribeLocalEvent]
    private void OnControlledSpeech(BeforeInGameICMessageEvent args)
    {
        if (args.Handled)
            return;
        var relay = new BodyControlSpeechRelayEvent(args.Source, args.Message);
        RaiseLocalEvent(relay);
        if (relay.Cancelled)
            return;
        var actor = args.Source;
        if (_handContext.TryGetContext(args.Source, out var context))
            actor = context.Controller;
        if (!TryGetControl(actor, out var body, out var access)
            || !TryComp<BodyControlSourceComponent>(actor, out var source) || source.ExternallyDriven)
            return;
        if (!access.Allows("body.speak"))
        {
            args.Handled = true;
            Deny(actor, "speak");
            return;
        }
        args.Source = body;
    }
}
