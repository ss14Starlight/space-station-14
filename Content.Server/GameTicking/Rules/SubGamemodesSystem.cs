using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage;

namespace Content.Server.GameTicking.Rules;

public sealed partial class SubGamemodesSystem : GameRuleSystem<SubGamemodesComponent> // Starlight edit
{
    [Dependency] private IChatManager _chat = default!; // Starlight
    [Dependency] private GameTicker _ticker = default!; // Starlight

    protected override void Added(EntityUid uid, SubGamemodesComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        var picked = EntitySpawnCollection.GetSpawns(comp.Rules, RobustRandom);
        foreach (var id in picked)
        {
            Log.Info($"Starting gamerule {id} as a subgamemode of {ToPrettyString(uid):rule}");
            GameTicker.AddGameRule(id);
            // Starlight begin
            if (_ticker is { RunLevel: GameRunLevel.PreRoundLobby, CurrentPreset: not null })
                _chat.SendAdminAnnouncement($"Preset {_ticker.CurrentPreset.ID}/{MetaData(uid).EntityPrototype!.ID} added {id} as a subgamemode.");
            // Starlight end
        }
    }
}
