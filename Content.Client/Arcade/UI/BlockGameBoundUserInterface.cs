using Content.Shared.Arcade.BlockGame;
using Robust.Client.UserInterface;

namespace Content.Client.Arcade.UI;

public sealed class BlockGameBoundUserInterface : BoundUserInterface
{
    private BlockGameMenu? _menu;

    public BlockGameBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<BlockGameMenu>();
        _menu.OnAction += SendAction;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        switch (message)
        {
            case BlockGameVisualUpdateMessage updateMessage: // Starlight-edit
                switch (updateMessage.GameVisualType)
                {
                    case BlockGameVisualType.GameField: // Starlight-edit
                        _menu?.UpdateBlocks(updateMessage.Blocks);
                        break;
                    case BlockGameVisualType.HoldBlock: // Starlight-edit
                        _menu?.UpdateHeldBlock(updateMessage.Blocks);
                        break;
                    case BlockGameVisualType.NextBlock: // Starlight-edit
                        _menu?.UpdateNextBlock(updateMessage.Blocks);
                        break;
                }
                break;
            case BlockGameScoreUpdateMessage scoreUpdate: // Starlight-edit
                _menu?.UpdatePoints(scoreUpdate.Points);
                break;
            case BlockGameUserStatusMessage userMessage: // Starlight-edit
                _menu?.SetUsability(userMessage.IsPlayer);
                break;
            case BlockGameSetScreenMessage statusMessage: // Starlight-edit
                if (statusMessage.IsStarted) _menu?.SetStarted();
                _menu?.SetScreen(statusMessage.Screen);
                if (statusMessage is BlockGameGameOverScreenMessage gameOverScreenMessage) // Starlight-edit
                    _menu?.SetGameoverInfo(gameOverScreenMessage.FinalScore, gameOverScreenMessage.LocalPlacement, gameOverScreenMessage.GlobalPlacement);
                break;
            case BlockGameHighScoreUpdateMessage highScoreUpdateMessage: // Starlight-edit
                // Starlight-start
                _menu?.UpdateHighscores(highScoreUpdateMessage.LocalHighscores,
                    highScoreUpdateMessage.GlobalHighscores,
                    highScoreUpdateMessage.MaxLocalScores,
                    highScoreUpdateMessage.MaxGlobalScores);
                // Starlight-end
                break;
            case BlockGameLevelUpdateMessage levelUpdateMessage: // Starlight-edit
                _menu?.UpdateLevel(levelUpdateMessage.Level);
                break;
        }
    }

    public void SendAction(BlockGamePlayerAction action)
    {
        SendMessage(new BlockGamePlayerActionMessage(action)); // Starlight-edit
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Dispose();
    }
}
