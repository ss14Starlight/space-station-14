// ReSharper disable CheckNamespace near the top of the file. - Making use of partials.
using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Shared.Database;

namespace Content.Server.Voting.Managers;

public sealed partial class VoteManager
{
    public IReadOnlyDictionary<string, int> GetPresetCooldowns() => _presetCooldown;

    public void AddPresetToCooldown(GamePresetPrototype preset)
    {
        if (preset.ID == SecretPrototype.Id) return;
        _presetCooldown[preset.ID] = preset.VoteCooldown;
        _adminLogger.Add(LogType.Vote, LogImpact.Medium,
            $"Preset {preset.ID} added to cooldown for {preset.VoteCooldown} votes.");
    }

    public void RemovePresetFromCooldown(GamePresetPrototype preset)
    {
        _presetCooldown.Remove(preset.ID);
        _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Preset {preset.ID} removed from cooldown.");
    }

    public void DecrementPresetCooldown(GamePresetPrototype preset)
    {
        foreach (var key in _presetCooldown.Keys.ToList().Where(key => key != preset.ID))
        {
            _presetCooldown[key]--;
            if (_presetCooldown[key] > 0) continue;
            _presetCooldown.Remove(key);
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Preset {key} removed from cooldown.");
        }
    }
}
