using Content.Server.EUI;
using Content.Shared.Starlight.NewLife;
using Content.Shared.Eui;
using Content.Shared.Ghost.Roles;

namespace Content.Server.Ghost.Roles.UI;

public sealed class NewLifeEui : BaseEui
{
    private readonly NewLifeSystem _newLifeSystem;
    private readonly HashSet<int> _usedSlots;
    private int _remainingLives;
    private int _maxLives;
    private TimeSpan _lastGhostTime;
    private TimeSpan _cooldown;
    public NewLifeEui(HashSet<int> usedSlots, int remainingLives, int maxLives, TimeSpan lastGhostTime, TimeSpan cooldown)
    {
        _newLifeSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NewLifeSystem>();
        _usedSlots = usedSlots;
        _remainingLives = remainingLives;
        _maxLives = maxLives;
        _lastGhostTime = lastGhostTime;
        _cooldown = cooldown;
    }

    public override NewLifeEuiState GetNewState() => new()
    {
        UsedSlots = _usedSlots,
        RemainingLives = _remainingLives,
        MaxLives = _maxLives,
        LastGhostTime = _lastGhostTime,
        Cooldown = _cooldown
    };

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
    }

    public override void Closed()
    {
        base.Closed();

        _newLifeSystem.CloseEui(Player);
    }
}
