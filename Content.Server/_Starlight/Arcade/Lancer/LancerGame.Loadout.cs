using System.Linq;
using Content.Shared._Starlight.Arcade.Lancer;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    private string _chassisId = ChassisRaijin;
    private string _playerSprite = "everest_blue";
    private LancerCoreKind _coreKind = LancerCoreKind.Raijin;
    private int _overwatchThreatRange = 1;
    private bool _hasVanguard;
    private bool _hasSentinel;
    private bool _hasNuclearCavalier;
    private int _playerGrit;
    private bool _hasExternalBatteries;
    private bool _externalBatteriesDestroyed;
    private bool _hasDeepWellHeatSink;
    private bool _deepWellHeatResistThisTurn;

    private int _playerMaxHp = 14;
    private int _playerMaxStructure = 4;
    private int _playerHeatCap = 6;
    private int _playerRepairCap = 6;
    private int _playerEvasion = 8;
    private int _playerSpeed = 4;
    private int _playerArmor;
    private int _playerHexCap = PlayerHexCharges;

    private readonly string[] _weaponIds = new string[WeaponSlotCount];
    private readonly bool[] _weaponLoaded = Enumerable.Repeat(true, WeaponSlotCount).ToArray();

    private string? _selectedLoadoutId;
    private int _pendingChoiceIndex = -1;

    /// <summary>Applies the selected mission loadout chassis, then permanent pilot skills.</summary>
    private void ApplyLoadoutAndSkills(LancerArcadeComponent? arcade, string? loadoutId = null)
    {
        var id = loadoutId ?? _selectedLoadoutId ?? LoadoutRaijinStrike;
        if (!MissionLoadouts.TryGetValue(id, out var loadout))
            loadout = MissionLoadouts[LoadoutRaijinStrike];

        _selectedLoadoutId = loadout.Id;
        ApplyChassis(loadout.ChassisId);

        // Mission-specific skins (e.g. deep-range Everest pistols) beat loadout/chassis defaults.
        if (_selectedMissionId != null
            && MissionLoadoutSprites.TryGetValue((_selectedMissionId, loadout.Id), out var missionSprite))
            _playerSprite = missionSprite;
        else if (!string.IsNullOrEmpty(loadout.SpriteState))
            _playerSprite = loadout.SpriteState!;

        // Optional weapon overrides for license-style Raijin variants.
        if (loadout.WeaponOverrides is { } overrides)
        {
            for (var i = 0; i < WeaponSlotCount && i < overrides.Length; i++)
            {
                if (!string.IsNullOrEmpty(overrides[i]))
                    _weaponIds[i] = overrides[i]!;
            }

            ResetWeaponLoadedState();
        }

        ApplyPilotSkills(arcade);
    }

    private void ApplyPilotSkills(LancerArcadeComponent? arcade)
    {
        var hull = arcade?.Hull ?? 0;
        var agility = arcade?.Agility ?? 0;
        var engineering = arcade?.Engineering ?? 0;

        _playerMaxHp += hull * 2;
        _playerSpeed += agility;
        _playerEvasion += agility / 2;
        _playerHeatCap += engineering;
        _playerHexCap = PlayerHexCharges + engineering / 2;
        // Grit = half LL rounded up (LicenseLevel tracks cleared missions).
        var ll = arcade?.LicenseLevel ?? 0;
        _playerGrit = (ll + 1) / 2;
    }

    private void ApplyChassis(string chassisId)
    {
        if (!Chassis.TryGetValue(chassisId, out var def))
            def = Chassis[ChassisRaijin];

        _chassisId = def.Id;
        _playerSprite = def.SpriteState;
        _playerMaxHp = def.MaxHp;
        _playerMaxStructure = def.MaxStructure;
        _playerHeatCap = def.HeatCap;
        _playerRepairCap = def.RepairCap;
        _playerEvasion = def.Evasion;
        _playerSpeed = def.Speed;
        _playerArmor = def.Armor;
        _coreKind = def.CoreKind;
        _overwatchThreatRange = def.OverwatchThreatRange;
        _hasVanguard = def.HasVanguard;
        _hasSentinel = def.HasSentinel;
        _hasNuclearCavalier = def.HasNuclearCavalier;
        _playerGrit = 0;
        _hasExternalBatteries = def.HasExternalBatteries;
        _externalBatteriesDestroyed = false;
        _hasDeepWellHeatSink = def.HasDeepWellHeatSink;
        _deepWellHeatResistThisTurn = false;
        _playerHexCap = PlayerHexCharges;

        for (var i = 0; i < WeaponSlotCount; i++)
            _weaponIds[i] = i < def.WeaponIds.Length ? def.WeaponIds[i] : string.Empty;

        ResetWeaponLoadedState();
    }

    private void ResetWeaponLoadedState()
    {
        // Loading weapons start loaded; firing empties them until Stabilize → Reload.
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            if (string.IsNullOrEmpty(_weaponIds[i]) || !Weapons.ContainsKey(_weaponIds[i]))
            {
                _weaponLoaded[i] = false;
                continue;
            }

            _weaponLoaded[i] = true;
        }
    }

    private LancerWeaponDef? GetWeaponDef(int index)
    {
        if (index < 0 || index >= WeaponSlotCount)
            return null;

        var id = _weaponIds[index];
        if (string.IsNullOrEmpty(id))
            return null;

        return Weapons.GetValueOrDefault(id);
    }

    private LancerWeaponDef? GetWeaponDefById(string id) =>
        Weapons.GetValueOrDefault(id);

    private int GetWeaponRange(int weaponIndex)
    {
        var def = GetWeaponDef(weaponIndex);
        if (def == null)
            return KnifeRange;

        return def.Range + GetTokugawaRangeBonus(def) + GetExternalBatteriesRangeBonus(def);
    }

    private int GetExternalBatteriesRangeBonus(LancerWeaponDef weapon)
    {
        if (!_hasExternalBatteries || _externalBatteriesDestroyed)
            return 0;
        if (!weapon.Tags.HasFlag(LancerWeaponTags.Energy))
            return 0;

        var isMelee = weapon.Tags.HasFlag(LancerWeaponTags.Melee) || weapon.Range <= 1;
        return isMelee ? 1 : 5;
    }

    private int GetTokugawaRangeBonus(LancerWeaponDef weapon)
    {
        if (_coreKind != LancerCoreKind.TokugawaRadiance)
            return 0;

        var isMelee = weapon.Tags.HasFlag(LancerWeaponTags.Melee) || weapon.Range <= 1;
        var energyEligible = weapon.Tags.HasFlag(LancerWeaponTags.Energy) || _playerExposed;

        // Radiance + Limit Break (Exposed) stack: +10 range / +3 threat.
        if (_coreActive && _playerExposed)
            return isMelee ? 3 : 10;

        // Limit Break alone: +5 range / +1 threat.
        if (_playerExposed)
            return isMelee ? 1 : 5;

        // Radiance alone: energy weapons +5 range / +2 threat.
        if (_coreActive && energyEligible)
            return isMelee ? 2 : 5;

        return 0;
    }

    private int GetTokugawaBonusDamage(LancerWeaponDef weapon)
    {
        if (_coreKind != LancerCoreKind.TokugawaRadiance || !_playerExposed)
            return 0;

        // Limit Break: +3 energy bonus damage while Exposed.
        return 3;
    }

    /// <summary>Danger Zone = heat at or above half of heat capacity (rounded up).</summary>
    private bool IsInDangerZone() =>
        _playerHeatCap > 0 && _heat * 2 >= _playerHeatCap;

    private void ActivateTokugawaRadiance()
    {
        // Radiance lasts the fight via _coreActive; Overclock grants Exposed until end of next turn.
        _playerExposed = true;
        _exposedTurnsRemaining = 2;
        AddLog(Loc.GetString("lancer-arcade-log-tokugawa-radiance"));
    }

    private void TickTokugawaExposed()
    {
        // Only tick Overclock Exposed (short duration). Permanent Exposed lasts until Stabilize.
        if (!_playerExposed
            || _exposedTurnsRemaining <= 0
            || _exposedTurnsRemaining >= PermanentExposedTurns)
            return;

        _exposedTurnsRemaining--;
        if (_exposedTurnsRemaining > 0)
            return;

        _playerExposed = false;
        AddLog(Loc.GetString("lancer-arcade-log-tokugawa-exposed-end"));
    }

    private static bool IsCqbWeapon(LancerWeaponDef weapon) =>
        weapon.Tags.HasFlag(LancerWeaponTags.Cqb);

    /// <summary>Vanguard Handshake Etiquette / See-Through Seeker range band.</summary>
    private static bool IsVanguardCqbBand(int distance) =>
        distance <= VanguardCqbRange;

    private bool IsAuxWeapon(int weaponIndex) =>
        GetWeaponDef(weaponIndex)?.Tags.HasFlag(LancerWeaponTags.Aux) ?? false;

    private bool IsArcingWeapon(int weaponIndex) =>
        GetWeaponDef(weaponIndex)?.Tags.HasFlag(LancerWeaponTags.Arcing) ?? false;

    private bool IsLoadingWeapon(int weaponIndex) =>
        GetWeaponDef(weaponIndex)?.Tags.HasFlag(LancerWeaponTags.Loading) ?? false;

    private bool IsOrdnanceWeapon(int weaponIndex) =>
        GetWeaponDef(weaponIndex)?.Tags.HasFlag(LancerWeaponTags.Ordnance) ?? false;

    private LancerWeaponBlockReason GetOrdnanceBlockReason(int weaponIndex)
    {
        if (!_weaponLoaded[weaponIndex])
            return LancerWeaponBlockReason.Empty;

        if (!IsOrdnanceWeapon(weaponIndex))
            return LancerWeaponBlockReason.None;

        if (_usedActions.Contains(LancerPlayerAction.SelectMove))
            return LancerWeaponBlockReason.Moved;

        if (_usedActions.Contains(LancerPlayerAction.SelectBoost))
            return LancerWeaponBlockReason.Boosted;

        var player = GetPlayerUnit();
        if (player != null && IsEngagedWithHostile(player))
            return LancerWeaponBlockReason.Engaged;

        return LancerWeaponBlockReason.None;
    }

    private string GetCampaignSummary(LancerArcadeComponent? arcade)
    {
        if (arcade == null)
            return Loc.GetString("lancer-campaign-summary-default");

        return Loc.GetString("lancer-campaign-summary",
            ("cleared", arcade.ClearedMissionIds.Count));
    }

    private string GetLoadoutSummary()
    {
        if (_selectedLoadoutId != null && MissionLoadouts.TryGetValue(_selectedLoadoutId, out var loadout))
            return Loc.GetString(loadout.NameLoc);

        var parts = new List<string>();
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            var def = GetWeaponDef(i);
            if (def != null)
                parts.Add(Loc.GetString(def.NameLoc));
        }

        return string.Join(" / ", parts);
    }
}
