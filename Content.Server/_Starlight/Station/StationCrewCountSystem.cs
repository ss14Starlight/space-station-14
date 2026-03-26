using System;
using System.Collections.Generic;
using System.Text;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Station;

public sealed class StationCrewCountSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager  = default!;

    /// <summary>
    /// Gets the total crew count in the round.
    /// </summary>

    public int GetTotalCrewCount()
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
                continue;
            count++;
        }
        return count;
    }
}