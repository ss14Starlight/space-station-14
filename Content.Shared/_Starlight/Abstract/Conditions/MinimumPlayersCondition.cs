using Robust.Shared.Player;

namespace Content.Shared._Starlight.Abstract.Conditions;

/// <summary>
/// Passes only when at least n amount of players are connected to the server.
/// </summary>
public sealed partial class MinimumPlayersCondition : BaseCondition
{
    [Dependency] private ISharedPlayerManager _players = default!;

    [DataField(required: true)]
    public int Players;

    public override bool Handle(EntityUid @subject, EntityUid @object)
    {
        base.Handle(@subject, @object);

        return _players.PlayerCount >= Players;
    }
}
