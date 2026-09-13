using Content.Shared._Starlight.Arcade.Lancer;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Arcade.Lancer;

public sealed class LancerArcadeBoundUserInterface : BoundUserInterface
{
    private LancerArcadeWindow? _window;

    public LancerArcadeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<LancerArcadeWindow>();
        _window.OnAction += SendAction;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        switch (message)
        {
            case LancerArcadeMessages.LancerGameStateMessage stateMessage:
                _window?.UpdateSnapshot(stateMessage.Snapshot);
                break;
            case LancerArcadeMessages.LancerLogMessage logMessage:
                _window?.AppendLog(logMessage.Line);
                break;
            case LancerArcadeMessages.LancerUserStatusMessage statusMessage:
                _window?.SetUsability(statusMessage.IsPlayer);
                break;
            case LancerArcadeMessages.LancerReactionPromptMessage reactionMessage:
                _window?.ShowReaction(reactionMessage.Reaction, reactionMessage.TimeoutSeconds, reactionMessage.PendingDamage);
                break;
            case LancerArcadeMessages.LancerDiceRollMessage diceMessage:
                _window?.EnqueueDiceRoll(diceMessage);
                break;
            case LancerArcadeMessages.LancerAttackEffectMessage effectMessage:
                _window?.PlayAttackEffect(effectMessage);
                break;
        }
    }

    private void SendAction(
        LancerPlayerAction action,
        LancerGridCoord? cell,
        int weaponIndex,
        int targetUnitId,
        LancerStabilizeOption stabilizeOption,
        string contextId)
    {
        SendMessage(new LancerArcadeMessages.LancerPlayerActionMessage(
            action,
            cell,
            weaponIndex,
            targetUnitId,
            stabilizeOption,
            contextId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
    }
}
