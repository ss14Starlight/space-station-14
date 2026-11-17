using Content.Shared.Ghost;
using Robust.Client.Player;

// ReSharper disable CheckNamespace
namespace Content.Client.Voting.UI;

public sealed partial class VotePopup
{
    [Dependency] private IPlayerManager _player = null!;
    [Dependency] private IEntityManager _entMan = null!;

    private bool IsGhost() => _entMan.HasComponent<GhostComponent>(_player.LocalEntity);
}
