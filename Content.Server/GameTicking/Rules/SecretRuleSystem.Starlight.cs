using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

public sealed partial class SecretRuleSystem : GameRuleSystem<SecretRuleComponent>
{
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

    private void RemovePresetCooldownOptions(Dictionary<string, float> options, bool includeSecretCooldowns)
    {
        foreach (var key in options.Keys.ToList())
        {
            var secretCooldown = includeSecretCooldowns
                ? _secretPresetCooldown.GetValueOrDefault(key)
                : 0;
            var dynamicCooldown = _dynamicRuleCooldown.TryGetPresetCooldown(
                new ProtoId<GamePresetPrototype>(key),
                out var remaining)
                ? remaining
                : 0;

            if (secretCooldown <= 0 && dynamicCooldown <= 0)
                continue;

            options.Remove(key);
            Log.Info(
                $"Preset {key} skipped for secret selection due to cooldown " +
                $"(Secret: {secretCooldown}, Dynamic: {dynamicCooldown} rounds remaining).");
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

        _dynamicRuleCooldown.ApplyPresetCooldown(pickedPreset);

        if (pickedPreset.VoteCooldown <= 0)
            return;

        _secretPresetCooldown[pickedPreset.ID] = pickedPreset.VoteCooldown;
        Log.Info($"Preset {pickedPreset.ID} added to secret cooldown for {pickedPreset.VoteCooldown} rounds.");
    }
}
