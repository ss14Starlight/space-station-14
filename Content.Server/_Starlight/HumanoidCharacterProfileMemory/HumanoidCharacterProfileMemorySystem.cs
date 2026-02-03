using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Robust.Shared.Network;

namespace Content.Server._Starlight.HumanoidCharacterProfileMemory;

public sealed class HumanoidCharacterProfileMemorySystem : EntitySystem
{
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _profiles = [];
    
    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    public bool TryGetActiveProfile(NetUserId user, [NotNullWhen(true)] out HumanoidCharacterProfile? profile) =>
        _profiles.TryGetValue(user, out profile);

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args) => _profiles[args.Player.UserId] = args.Profile;

    private void OnRestart(RoundRestartCleanupEvent args) => _profiles.Clear();
}