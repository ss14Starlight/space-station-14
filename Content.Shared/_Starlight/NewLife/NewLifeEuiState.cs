using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.NewLife;

[NetSerializable, Serializable]
public sealed class NewLifeEuiState : EuiStateBase
{
    public HashSet<int> UsedSlots { get; set; } = [];
    public int RemainingLives { get; set; }
    public int MaxLives { get; set; }
    public TimeSpan LastGhostTime { get; set; }
    public TimeSpan Cooldown { get; set; }
}
[NetSerializable, Serializable]
public sealed class NewLifeOpenedEvent : EntityEventArgs
{
}
