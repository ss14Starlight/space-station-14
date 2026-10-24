using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Actions.Components;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;

namespace Content.Client._Starlight.BodyControl;

public sealed partial class BodyControlSystem
{
    private BodyControlWindow? _controlWindow;
    private EntityUid? _presentedBody;
    private bool _presentationHidden;

    private void UpdatePresentation()
    {
        if (!TryComp<BodyControlSourceComponent>(_controlPlayer.LocalEntity, out var source)
            || source.ExternallyDriven || source.Body == _controlPlayer.LocalEntity && source.Access.FullControl
            || TerminatingOrDeleted(source.Body))
        {
            ShutdownPresentation();
            return;
        }
        if (_presentedBody != source.Body)
        {
            ShutdownPresentation();
            _presentedBody = source.Body;
        }
        if (_presentationHidden)
            return;
        if (_controlWindow == null)
        {
            _controlWindow = new BodyControlWindow(EntityManager);
            _controlWindow.OnCommand += command => SendControlCommand(command);
            _controlWindow.OnAction += action =>
            {
                if (HasComp<TargetActionComponent>(action))
                    SelectTargetAction(action);
                else
                    SendControlCommand(new BodyControlCommandEvent(BodyControlCommand.Action, action: GetNetEntity(action)));
            };
            _controlWindow.OnClose += () => _presentationHidden = true;
            _controlWindow.OpenCentered();
        }
        _controlWindow.Title = Loc.GetString("body-control-panel-title") + " — " + Name(source.Body);
        var hands = new List<BodyControlWindow.HandEntry>();
        if (TryComp<HandsComponent>(source.Body, out var bodyHands))
        {
            foreach (var hand in bodyHands.SortedHands)
            {
                if (!_handContext.AllowsHand(source.Body, hand, source.Access))
                    continue;
                _controlHands.TryGetHeldItem((source.Body, bodyHands), hand, out var item);
                hands.Add(new(hand, bodyHands.Hands[hand].Location, item,
                    item is { } held ? Name(held) : Loc.GetString("body-control-panel-empty"),
                    bodyHands.Hands[hand].EmptyRepresentative));
            }
        }
        var actions = new List<EntityUid>();
        if (source.Access.Allows("body.actions"))
        {
            foreach (var action in _controlActions.GetActions(source.Body))
            {
                if (_controlActions.GetEvent(action) is OpenMindCollectiveEvent
                    || !source.Access.FullControl && source.Access.Actions != null
                    && (MetaData(action).EntityPrototype is not { } prototype || !source.Access.Actions.Contains(prototype.ID)))
                    continue;
                actions.Add(action);
            }
        }
        bool? combat = source.Access.Allows("body.interact") && TryComp<CombatModeComponent>(source.Body, out var mode)
            ? mode.IsInCombatMode : null;
        _controlWindow.UpdateState(hands, actions, source.SelectedHand, combat);
    }

    private void ShutdownPresentation()
    {
        _controlWindow?.Dispose();
        _controlWindow = null;
        _presentedBody = null;
        _presentationHidden = false;
    }
}
