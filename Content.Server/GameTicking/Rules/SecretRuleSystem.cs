using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        _adminLogger.Add(LogType.EventStarted, $"Selected {preset.ID} as the secret preset.");

        foreach (var rule in preset.Rules)
        {
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
        var baseOptions = _prototypeManager.Index(weights).Weights.ShallowClone();
        var players = GameTicker.ReadyPlayerCount();

        Log.Info(
            $"Secret roll pool: weights={weights}, players={players}, " +
            $"optionCount={baseOptions.Count}, rawSum={baseOptions.Values.Sum()}, " +
            $"cooldowns=[{string.Join(", ", _secretPresetCooldown.Select(x => $"{x.Key}:{x.Value}"))}], " +
            $"options=[{string.Join(", ", baseOptions.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}"))}]");

        var options = baseOptions.ShallowClone();
        RemoveSecretCooldownOptions(options);

        if (TryPickPresetFromOptions(options, weights, players, out preset))
        {
            UpdateSecretPresetCooldown(preset);
            return true;
        }

        Log.Warning("Secret preset cooldown removed every valid option. Retrying without cooldowns.");

        options = baseOptions.ShallowClone();

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

        foreach (var ruleId in selected.Rules)
        {
            if (!_prototypeManager.TryIndex(ruleId, out EntityPrototype? rule)
                || !rule.TryGetComponent(_ruleCompName, out GameRuleComponent? ruleComp))
            {
                Log.Error($"Encountered invalid rule {ruleId} in preset {selected.ID}");
                return false;
            }

            if (ruleComp.MinPlayers > players && ruleComp.CancelPresetOnTooFewPlayers)
                return false;
        }

        return true;
    }

    #region Starlight
    private bool TryPickPresetFromOptions(
        Dictionary<string, float> options,
        ProtoId<WeightedRandomPrototype> weights,
        int players,
        [NotNullWhen(true)] out GamePresetPrototype? preset)
    {
        var attempt = 0;

        while (options.Count > 0)
        {
            attempt++;

            var sum = options.Values.Sum();

            if (sum <= 0f)
            {
                Log.Error($"Secret preset weights {weights} had no positive remaining weight.");
                break;
            }

            var accumulated = 0f;
            var rand = _random.NextFloat(sum);
            string? selectedId = null;
            var selectedWeight = 0f;

            foreach (var (key, weight) in options)
            {
                accumulated += weight;

                if (accumulated < rand)
                    continue;

                selectedId = key;
                selectedWeight = weight;
                break;
            }

            if (selectedId == null)
            {
                Log.Error($"Secret preset weights {weights} failed to pick a candidate despite having options.");
                break;
            }

            options.Remove(selectedId);

            if (!_prototypeManager.TryIndex(selectedId, out GamePresetPrototype? selectedPreset))
            {
                Log.Error($"Invalid preset {selectedId} in secret rule weights: {weights}");
                continue;
            }

            var canPick = CanPick(selectedPreset, players);

            Log.Info(
                $"Secret roll attempt {attempt}: weights={weights}, players={players}, " +
                $"rand={rand}, rollSum={sum}, selected={selectedId}, " +
                $"selectedWeight={selectedWeight}, canPick={canPick}, remaining={options.Count}");

            if (canPick)
            {
                preset = selectedPreset;
                return true;
            }

            Log.Info($"Excluding {selectedPreset.ID} from secret preset selection.");
        }

        preset = null;
        return false;
    }

    private void RemoveSecretCooldownOptions(Dictionary<string, float> options)
    {
        foreach (var key in options.Keys.ToList())
        {
            if (!_secretPresetCooldown.TryGetValue(key, out var cooldown))
                continue;

            options.Remove(key);
            Log.Info($"Preset {key} skipped for secret selection due to cooldown ({cooldown} rounds remaining).");
        }
    }

    private void UpdateSecretPresetCooldown(GamePresetPrototype pickedPreset)
    {
        foreach (var key in _secretPresetCooldown.Keys.ToList())
        {
            if (key == pickedPreset.ID)
                continue;

            _secretPresetCooldown[key]--;

            if (_secretPresetCooldown[key] > 0)
                continue;

            _secretPresetCooldown.Remove(key);
            Log.Info($"Preset {key} removed from secret cooldown.");
        }

        if (pickedPreset.VoteCooldown <= 0)
            return;

        _secretPresetCooldown[pickedPreset.ID] = pickedPreset.VoteCooldown;
        Log.Info($"Preset {pickedPreset.ID} added to secret cooldown for {pickedPreset.VoteCooldown} rounds.");
    }
    #endregion
}
