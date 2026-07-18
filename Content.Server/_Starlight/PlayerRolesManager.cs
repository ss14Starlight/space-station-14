using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._NullLink;
using Content.Shared._Starlight;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Starlight;

public sealed partial class PlayerRolesManager : IPlayerRolesManager, IPostInjectInit
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IServerDbManager _dbManager = default!;
    [Dependency] private IServerNetManager _netMgr = default!;
    [Dependency] private ILogManager _logger = default!;
    [Dependency] private ISharedNullLinkPlayerResourcesManager _playerResources = default!;

    private readonly Dictionary<ICommonSession, PlayerReg> _players = new();

    public IEnumerable<PlayerReg> Players => _players.Values;

    private ISawmill _sawmill = default!;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgUpdatePlayerStatus>();
        _sawmill = _logger.GetSawmill("player_manager");
    }

    void IPostInjectInit.PostInject()
        => _playerManager.PlayerStatusChanged += PlayerStatusChanged;

    private void PlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected)
            UpdatePlayerStatus(e.Session);
        else if (e.NewStatus == SessionStatus.InGame)
            Login(e.Session);
        else if (e.NewStatus == SessionStatus.Disconnected)
        {
            var balance = 0.0;
            _playerResources.TryGetResource(e.Session, "credits", out var creditBalance);
            if (creditBalance is { } credits)
                balance = credits;

            _playerResources.RemoveResources(e.Session, out _);

            if (_players.Remove(e.Session, out var data))
            {
                _ = _dbManager.SetPlayerDataForAsync(e.Session.UserId, new StarLightModel.PlayerDataDTO
                {
                    GhostTheme = data!.Data.GhostTheme,
                    Balance = (int) balance
                });
            }
        }
    }
    private void UpdatePlayerStatus(ICommonSession session)
    {
        var userid = session.UserId;
        var msg = new MsgUpdatePlayerStatus();

        if (_players.TryGetValue(session, out var playerData))
            msg.Player = playerData.Data;

        _netMgr.ServerSendMessage(msg, session.Channel);
    }

    private async void Login(ICommonSession session)
    {
        var adminDat = await LoadPlayerData(session);

        // Player may have disconnected while awaiting the database.
        if (session.Status == SessionStatus.Disconnected)
            return;

        var reg = new PlayerReg(session, adminDat.Data);

        if (!_players.TryAdd(session, reg))
            return;

        _playerResources.TrySetResource(session, "credits", adminDat.Balance, skipNullLink: true);

        UpdatePlayerStatus(session);
    }

    private async Task<(PlayerData Data, int Balance)> LoadPlayerData(ICommonSession session)
    {
        var dbData = await _dbManager.GetPlayerDataForAsync(session.UserId);

        if (dbData == null)
        {
            dbData = new StarLightModel.PlayerDataDTO
            {
                UserId = session.UserId,
                Balance = 500,
                GhostTheme = "None",
            };
            await _dbManager.SetPlayerDataForAsync(session.UserId, dbData);
        }

        var data = new PlayerData
        {
            Title = dbData.Title,
            GhostTheme = dbData.GhostTheme
        };

        return (data, dbData.Balance);
    }
    public PlayerData? GetPlayerData(EntityUid uid)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session)) return null;
        return GetPlayerData(session);
    }

    public PlayerData? GetPlayerData(ICommonSession session) => _players.TryGetValue(session, out var data) ? data.Data : null;

    public sealed class PlayerReg(ICommonSession session, PlayerData data)
    {
        public readonly ICommonSession Session = session;

        public PlayerData Data = data;
    }
}
