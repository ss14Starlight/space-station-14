using Content.Server.EUI;
using Content.Server.Ghost.Roles.UI;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Network;
using Content.Server.RoundEnd;
using Content.Server.GameTicking;
using Content.Shared._Starlight.CCVar;

namespace Content.Server._Starlight.NewLife;

[UsedImplicitly]
public sealed partial class NewLifeSystem : EntitySystem
{
    [Dependency] private EuiManager _euiManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private readonly Dictionary<ICommonSession, NewLifeEui> _openUis = [];
    private readonly Dictionary<NetUserId, HashSet<int>> _roundCharactersUsed = [];
    private readonly Dictionary<NetUserId, int> _newLifesLeft = [];
    private readonly Dictionary<NetUserId, TimeSpan> _lastGhostTime = [];
    private int _maxNewLifes = 5;
    private TimeSpan _cooldown;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndSystemChangedEvent>(ClearRoundCharacterUsed);
        //cvar for max new lifes
        _configuration.OnValueChanged(StarlightCCVars.MaxNewLifes, UpdateMaxNewLifes, true);
        _configuration.OnValueChanged(StarlightCCVars.NewLifeGhostCooldown, UpdateCooldown, true);
    }

    private void UpdateMaxNewLifes(int value)
    {
        _maxNewLifes = value;
        //update all open uis
        UpdateAllEui();
    }

    private void UpdateCooldown(int value)
    {
        _cooldown = TimeSpan.FromSeconds(value);
        //update all open uis
        UpdateAllEui();
    }

    public override void Shutdown()
        => base.Shutdown();

    public void ClearRoundCharacterUsed(RoundEndSystemChangedEvent _)
    {
        if (_gameTicker.RunLevel == GameRunLevel.PreRoundLobby)
        {
            _roundCharactersUsed.Clear();
            _newLifesLeft.Clear();
            _lastGhostTime.Clear();
        }
    }

    public void OpenEui(ICommonSession session)
    {
        if (session.AttachedEntity is not { Valid: true } attached ||
            !EntityManager.HasComponent<GhostComponent>(attached))
            return;

        if (_openUis.ContainsKey(session))
            CloseEui(session);

        var usedSlots = _roundCharactersUsed.TryGetValue(session.UserId, out var slots) ? slots : [];
        var remainingLives = _newLifesLeft.TryGetValue(session.UserId, out var remaining) ? remaining : _maxNewLifes;
        var lastGhostTime = _lastGhostTime.TryGetValue(session.UserId, out var last) ? last : TimeSpan.Zero;
        var eui = _openUis[session] = new NewLifeEui(usedSlots, remainingLives, _maxNewLifes, lastGhostTime, _cooldown);

        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }
    public void CloseEui(ICommonSession session)
    {
        if (!_openUis.ContainsKey(session))
            return;

        _openUis.Remove(session, out var eui);

        eui?.Close();
    }
    public void UpdateAllEui()
    {
        foreach (var eui in _openUis.Values)
        {
            eui.StateDirty();
        }
    }

    public override void Update(float frameTime)
        => base.Update(frameTime);

    public void SaveGhostTime(NetUserId userId, TimeSpan time)
    {
        if (_lastGhostTime.ContainsKey(userId))
            _lastGhostTime[userId] = time;
        else
            _lastGhostTime.Add(userId, time);
    }

    internal void SaveCharacterToUsed(NetUserId userId, int slot)
    {
        if (_roundCharactersUsed.TryGetValue(userId, out var characters))
            characters.Add(slot);
        else
            _roundCharactersUsed.Add(userId, [slot]);

        //subtract from remaining slots
        if (_newLifesLeft.TryGetValue(userId, out var remaining))
        {
            remaining--;
            _newLifesLeft[userId] = remaining;
        }
        else
        {
            _newLifesLeft.Add(userId, _maxNewLifes - 1);
        }
    }
    internal bool SlotIsAvailable(NetUserId userId, int slot)
        => (!_roundCharactersUsed.TryGetValue(userId, out var characters))
        || !characters.Contains(slot);
}

[AnyCommand]
public sealed partial class NewLife : IConsoleCommand
{
    [Dependency] private IEntityManager _e = default!;

    public string Command => "newlife";
    public string Description => "Opens the new life request window.";
    public string Help => $"{Command}";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player != null)
            _e.System<NewLifeSystem>().OpenEui(shell.Player);
        else
            shell.WriteLine("You can only open the new life UI on a client.");
    }
}
