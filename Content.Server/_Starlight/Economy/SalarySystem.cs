using System.Linq;
using Content.Server._NullLink.PlayerData;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._NullLink;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.Starlight.SecureTerminal;

namespace Content.Shared.Starlight.Economy;
public sealed partial class SalarySystem : SharedSalarySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly INullLinkPlayerManager _nullLinkRoles = default!;
    [Dependency] private readonly IPlayerRolesManager _playerRolesManager = default!;
    [Dependency] private readonly ISharedNullLinkPlayerResourcesManager _playerResources = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;

    private float _delayAccumulator = 0f;
    private readonly Stopwatch _stopwatch = new();
    private readonly Dictionary<ICommonSession, TimeSpan> _lastSalary = [];
    private SalariesPrototype _salaries = new();
    private float _defaultBonusMultiplier = 1.0f;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartingEvent>(ev => _lastSalary.Clear());
        _configurationManager.OnValueChanged(StarlightCCVars.SalaryMultiplier, UpdateBonusMultiplier, true);

        _salaries = _prototypes.Index<SalariesPrototype>("standart");

        base.Initialize();
    }

    private void UpdateBonusMultiplier(float value)
        => _defaultBonusMultiplier = value;

    public override void Update(float frameTime)
    {
        _delayAccumulator += frameTime;
        if (_delayAccumulator > 2)
        {
            _delayAccumulator = 0;
            _stopwatch.Restart();

            var query = _playerRolesManager.Players.GetEnumerator();
            while (query.MoveNext() && query.Current != null && _stopwatch.Elapsed < TimeSpan.FromMilliseconds(0.1))
            {
                if (!_lastSalary.TryGetValue(query.Current.Session, out var lastTime))
                {
                    _lastSalary.Add(query.Current.Session, _time.CurTime);
                    continue;
                }
                if (!_entityManager.TryGetComponent<MobStateComponent>(query.Current.Session.AttachedEntity, out var state)
                    || state.CurrentState == MobState.Critical
                    || state.CurrentState == MobState.Dead)
                    continue;
                if (_time.CurTime - lastTime > TimeSpan.FromMinutes(15)
                    && _mind.TryGetMind(query.Current.Session.UserId, out var mind))
                {

                    var roles = _roles.MindGetAllRoleInfo((mind.Value.Owner, mind.Value.Comp));
                    foreach (var role in roles)
                    {
                        if (_salaries.Jobs.TryGetValue(role.Prototype, out var salary)
                            && _playerResources.TryGetResource(query.Current.Session, "credits", out var balance))
                        {
                            var amount = CalculateSalaryWithBonuses(salary, query.Current.Session);

                            _playerResources.TryUpdateResource(query.Current.Session, "credits", amount);
                            var message = Loc.GetString("economy-chat-salary-message", ("amount", amount), ("sender", "NanoTrasen"));
                            var wrappedMessage = Loc.GetString("economy-chat-salary-wrapped-message", ("amount", amount), ("sender", "NanoTrasen"), ("senderColor", "#2384CE"));
                            _chat.ChatMessageToOne(ChatChannel.Notifications, message, wrappedMessage, default, false, query.Current.Session.Channel, Color.FromHex("#57A3F7"));
                        }
                    }

                    _lastSalary[query.Current.Session] = _time.CurTime;
                }
            }
        }
    }

    private int CalculateSalaryWithBonuses(int baseSalary, ICommonSession session)
    {
        var bonusMultiplier = _defaultBonusMultiplier;

        if (!_nullLinkRoles.TryGetPlayerData(session.UserId, out var playerData))
            return baseSalary;

        foreach (var bonus in _prototypes.EnumeratePrototypes<SalaryRoleBonusPrototype>())
            if(bonus.Roles.Any(playerData.Roles.Contains))
                bonusMultiplier += bonus.Multiplayer;

        var stationPenalty = GetStationSalaryPenalty();
        return (int)Math.Ceiling(baseSalary * bonusMultiplier * (1f - stationPenalty));
    }

    // TODO: Add a way to support multistation? or we do this global? (maybe global as they might be on same map and so benefit)
    private float GetStationSalaryPenalty()
    {
        var maxPenalty = 0f;
        var query = _entityManager.EntityQueryEnumerator<SecureCommandTerminalStationComponent>();
        while (query.MoveNext(out _, out var comp))
            maxPenalty = Math.Max(maxPenalty, comp.SalaryPenalty);
        return maxPenalty;
    }

    internal void Donate(ICommonSession session, int amount)
    {
        if (!_playerResources.TryGetResource(session, "credits", out var balance))
            return;

        _playerResources.TryUpdateResource(session, "credits", amount);

        // We need to make a prototype
        var i = _random.Next(0, 20);
        var message = Loc.GetString($"economy-chat-donate-{i}-message", ("amount", amount));
        var wrappedMessage = Loc.GetString($"economy-chat-donate-{i}-wrapped-message", ("amount", amount));
        _chat.ChatMessageToOne(ChatChannel.Notifications, message, wrappedMessage, default, false, session.Channel, Color.FromHex("#57A3F7"));
    }
}
