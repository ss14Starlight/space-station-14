using System.Linq;
using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Starlight.Admeme.Jumpscare;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;

namespace Content.Server._Starlight.Admeme.Jumpscare;

public sealed partial class JumpscarePanelEui : BaseEui
{
    [Dependency] private IAdminLogManager _aLog = null!;
    [Dependency] private IAdminManager _admin = null!;
    [Dependency] private IEntityManager _ent = null!;
    [Dependency] private IPlayerManager _player = null!;
    private readonly MobStateSystem _mobState;
    private readonly JumpscareSystem _jumpscare;
    private readonly AutoDiscordLogSystem _dLog;

    /// <summary>
    /// If set, this panel can only jumpscare this entity. Otherwise, targets are picked by the client.
    /// </summary>
    public readonly EntityUid? LockedTarget;

    public JumpscarePanelEui(EntityUid? lockedTarget = null)
    {
        IoCManager.InjectDependencies(this);
        _mobState = _ent.System<MobStateSystem>();
        _jumpscare = _ent.System<JumpscareSystem>();
        _dLog = _ent.System<AutoDiscordLogSystem>();
        LockedTarget = lockedTarget;
    }

    public override void Opened()
    {
        base.Opened();
        _admin.OnPermsChanged += OnPermsChanged;
        _jumpscare.AddPanel(this);
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _admin.OnPermsChanged -= OnPermsChanged;
        _jumpscare.RemovePanel(this);
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admin.HasAdminFlag(Player, AdminFlags.Fun))
            Close();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Fun))
            return;

        if (msg is JumpscareRefreshPlayersMessage)
        {
            StateDirty();
            return;
        }

        if (msg is not JumpscareTargetMessage jumpscare)
            return;

        if (LockedTarget is { } locked)
        {
            if (_ent.Deleted(locked))
            {
                Close();
                return;
            }

            _jumpscare.Jumpscare(locked, jumpscare.Jumpscare);
            _aLog.Add(LogType.AdminCommands, LogImpact.Extreme,
                $"{Player.Name} executed jumpscare on {_ent.ToPrettyString(locked)}.");
            _dLog.LogToDiscord($"{Player.Name} executed jumpscare on {locked}.");
            return;
        }

        List<EntityUid> succeeded = [];
        foreach (var netEnt in jumpscare.Targets)
        {
            if (!_ent.TryGetEntity(netEnt, out var target) || _ent.Deleted(target))
                continue;

            succeeded.Add(target.Value);
            _jumpscare.Jumpscare(target.Value, jumpscare.Jumpscare);
        }

        var str = string.Join(", ", succeeded.Select(uid => $"{_ent.ToPrettyString(uid)}"));
        _aLog.Add(LogType.AdminCommands, LogImpact.Extreme,
            $"{Player.Name} executed jumpscare on {str}.");
        _dLog.LogToDiscord($"{Player.Name} executed jumpscare on {str}.");
    }

    public override EuiStateBase GetNewState()
    {
        var players = new List<JumpscarePlayerInfo>();
        if (LockedTarget != null) return new JumpscarePanelEuiState(_ent.GetNetEntity(LockedTarget), players);
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;

            var isGhost = _ent.HasComponent<GhostComponent>(ent);
            players.Add(new JumpscarePlayerInfo(
                _ent.GetNetEntity(ent),
                session.Name,
                _ent.GetComponent<MetaDataComponent>(ent).EntityName,
                isGhost,
                !isGhost && _mobState.IsAlive(ent)));
        }

        return new JumpscarePanelEuiState(_ent.GetNetEntity(LockedTarget), players);
    }
}
