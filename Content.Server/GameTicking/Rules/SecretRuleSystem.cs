using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Starlight.GameTicking.Rules;
using Content.Server._Starlight.Statistics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules;

public sealed partial class SecretRuleSystem : GameRuleSystem<SecretRuleComponent>
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chatManager = default!; // Starlight
    [Dependency] private GameTicker _ticker = default!;  // Starlight
    [Dependency] private DynamicRuleCooldownSystem _dynamicRuleCooldown = default!;  // Starlight
    [Dependency] private RoundStatisticsSystem _roundStatistics = default!;  // Starlight

    private readonly Dictionary<string, int> _secretPresetCooldown = new();
    private string _ruleCompName = default!;

    public override void Initialize()
    {
        base.Initialize();
        _ruleCompName = Factory.GetComponentName<GameRuleComponent>();
    }

    protected override void Added(EntityUid uid, SecretRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
        var weights = _configurationManager.GetCVar(CCVars.SecretWeightPrototype);

        if (!TryPickPreset(weights, out var preset))
        {
            Log.Error($"{ToPrettyString(uid)} failed to pick any preset. Removing rule.");
            Del(uid);
            return;
        }

        Log.Info($"Selected {preset.ID} as the secret preset.");
        _roundStatistics.RecordResolvedPreset(preset.ID); // Starlight
        if (_ticker.RunLevel == GameRunLevel.PreRoundLobby) _chatManager.SendAdminAnnouncement($"Round preset selected: Secret ({preset.ID})."); // Starlight
        _adminLogger.Add(LogType.EventStarted, $"Selected {preset.ID} as the secret preset.");

        foreach (var rule in preset.Rules)
        {
            if (GameTicker.IsIgnored(rule))
                continue;

            EntityUid ruleEnt;

            // if we're pre-round (i.e. will only be added)
            // then just add rules. if we're added in the middle of the round (or at any other point really)
            // then we want to start them as well
            if (GameTicker.RunLevel <= GameRunLevel.InRound)
                ruleEnt = GameTicker.AddGameRule(rule);
            else
                GameTicker.StartGameRule(rule, out ruleEnt);

            component.AdditionalGameRules.Add(ruleEnt);
        }
    }

    protected override void Ended(EntityUid uid, SecretRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        foreach (var rule in component.AdditionalGameRules)
        {
            GameTicker.EndGameRule(rule);
        }
    }

    private bool TryPickPreset(ProtoId<WeightedRandomPrototype> weights, [NotNullWhen(true)] out GamePresetPrototype? preset)
    {
        // Starligth edit Start: Extra Logging and Cooldown
        _dynamicRuleCooldown.EnsureRoundInitialized(dynamicRound: false);
        var baseOptions = _prototypeManager.Index(weights).Weights.ShallowClone();
        var players = GameTicker.ReadyPlayerCount();

        Log.Info(
            $"Secret roll pool: weights={weights}, players={players}, " +
            $"optionCount={baseOptions.Count}, rawSum={baseOptions.Values.Sum()}, " +
            $"cooldowns=[{string.Join(", ", _secretPresetCooldown.Select(x => $"{x.Key}:{x.Value}"))}], " +
            $"options=[{string.Join(", ", baseOptions.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}"))}]");

        var options = baseOptions.ShallowClone();
        RemovePresetCooldownOptions(options, includeSecretCooldowns: true);

        if (TryPickPresetFromOptions(options, weights, players, out preset))
        {
            UpdateSecretPresetCooldown(preset);
            return true;
        }

        Log.Warning("Preset cooldowns removed every valid option. Retrying without Secret's own cooldowns.");

        options = baseOptions.ShallowClone();
        RemovePresetCooldownOptions(options, includeSecretCooldowns: false);

        if (TryPickPresetFromOptions(options, weights, players, out preset))
        {
            UpdateSecretPresetCooldown(preset);
            return true;
        }
        // Starlight edit End
        return false;
    }

    public bool CanPickAny()
    {
        var secretPresetId = _configurationManager.GetCVar(CCVars.SecretWeightPrototype);
        return CanPickAny(secretPresetId);
    }

    /// <summary>
    /// Can any of the given presets be picked, taking into account the currently available player count?
    /// </summary>
    public bool CanPickAny(ProtoId<WeightedRandomPrototype> weightedPresets)
    {
        var ids = _prototypeManager.Index(weightedPresets).Weights.Keys
            .Select(x => new ProtoId<GamePresetPrototype>(x));

        return CanPickAny(ids);
    }

    /// <summary>
    /// Can any of the given presets be picked, taking into account the currently available player count?
    /// </summary>
    public bool CanPickAny(IEnumerable<ProtoId<GamePresetPrototype>> protos)
    {
        var players = GameTicker.ReadyPlayerCount();
        foreach (var id in protos)
        {
            if (!_prototypeManager.TryIndex(id, out var selectedPreset))
                Log.Error($"Invalid preset {selectedPreset} in secret rule weights: {id}");

            if (CanPick(selectedPreset, players))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Can the given preset be picked, taking into account the currently available player count?
    /// </summary>
    private bool CanPick([NotNullWhen(true)] GamePresetPrototype? selected, int players)
    {
        if (selected == null)
            return false;

        return players >= GameTicker.GetMinimumPlayerCount(selected);
    }
}
