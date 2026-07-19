using Content.Shared._Starlight.Arcade.Lancer;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    public const int GridSize = LancerHex.GridSize;

    public static readonly LancerGridCoord RelayPosition = new(2, 5);
    public static readonly LancerGridCoord RubbleSoftPosition = new(3, 4);
    public static readonly LancerGridCoord RubbleHardPosition = new(5, 6);
    public static readonly LancerGridCoord PlayerDeploy = new(2, 10);

    public const int RelayMaxHp = 20;
    public const int RelayEvasion = 5;

    public const int PlayerMaxStress = 4;
    public const int PlayerHexCharges = 3;
    public const int LockOnRange = 10;
    /// <summary>Exposed-until-Stabilize sentinel used by overheat / meltdown outcomes.</summary>
    public const int PermanentExposedTurns = 99;
    public const int WeaponSlotCount = 3;

    public const int GruntMaxHp = 1;
    public const int GruntArmor = 1;
    public const int GruntEvasion = 8;
    public const int GruntAccBonus = 0;

    public const int CutlassMaxHp = 15;
    public const int CutlassArmor = 1;
    public const int CutlassEvasion = 8;
    public const int CutlassAccBonus = 1;
    public const int CutlassHunkerReduction = 6;

    public const int AmrRange = 15;
    public const int RifleRange = 10;
    public const int KnifeRange = 1;

    public static readonly LancerGridCoord GruntOneStart = new(1, 3);
    public static readonly LancerGridCoord GruntTwoStart = new(3, 3);
    public static readonly LancerGridCoord CutlassStart = new(4, 2);

    public const float AiStepDelay = 0.8f;
    public const float ReactionTimeout = 10f;
    public const float RollDisplayDelay = 1.2f;

    // Loadout ids (mission-start choices)
    public const string LoadoutRaijinStrike = "raijin-strike";
    public const string LoadoutRaijinLicense = "raijin-license";
    public const string LoadoutTortuga = "tortuga";
    public const string LoadoutMonarch = "monarch";
    public const string LoadoutTokugawa = "tokugawa";

    // Skill pick ids
    public const string SkillHull = "hull";
    public const string SkillAgility = "agility";
    public const string SkillEngineering = "engineering";

    // Chassis ids
    public const string ChassisRaijin = "raijin";
    public const string ChassisTortuga = "tortuga";
    public const string ChassisMonarch = "monarch";
    public const string ChassisTokugawa = "tokugawa";

    // Weapon ids
    public const string WeaponAmr = "amr";
    public const string WeaponAssaultRifle = "assault-rifle";
    public const string WeaponKnife = "knife";
    public const string WeaponSegmentKnife = "segment-knife";
    public const string WeaponCannibal = "cannibal";
    public const string WeaponDeckSweeper = "deck-sweeper";
    public const string WeaponCatalyticHammer = "catalytic-hammer";
    public const string WeaponJavelin = "javelin";
    public const string WeaponPinaka = "pinaka";
    public const string WeaponAnnihilator = "annihilator";
    public const string WeaponTorch = "torch";
    public const string WeaponGruntRifle = "grunt-rifle";
    public const string WeaponCutlassRifle = "cutlass-rifle";
    public const string WeaponAssaultRifleNpc = "assault-npc-rifle";
    public const string WeaponSniperAmr = "sniper-amr";
    public const string WeaponBombard = "bombard-mortar";

    /// <summary>Vanguard Handshake / See-Through Seeker CQB band (spaces).</summary>
    public const int VanguardCqbRange = 3;

    [Flags]
    public enum LancerWeaponTags : ushort
    {
        None = 0,
        Ordnance = 1 << 0,
        Loading = 1 << 1,
        Arcing = 1 << 2,
        Aux = 1 << 3,
        Energy = 1 << 4,
        Melee = 1 << 5,
        Ap = 1 << 6,
        Overkill = 1 << 7,
        /// <summary>Close Quarters Battle weapon (shotguns, etc.). Used by Vanguard talents.</summary>
        Cqb = 1 << 8,
        /// <summary>Inaccurate: +1 difficulty on the attack roll.</summary>
        Inaccurate = 1 << 9,
        /// <summary>On crit: target must save or become Stunned (Catalytic Hammer).</summary>
        StunOnCrit = 1 << 10,
    }

    public enum LancerCoreKind : byte
    {
        Raijin,
        TortugaSentinel,
        MonarchDivine,
        TokugawaRadiance,
    }

    public sealed class LancerWeaponDef
    {
        public required string Id;
        public required string NameLoc;
        public int Range;
        public int DamageDice = 1;
        public int DamageSides = 6;
        public int DamageFlat;
        public int ReliableMiss;
        public LancerWeaponTags Tags;
        public int AccWithin;
        public int BlastRadius;
        /// <summary>Heat applied to the attacker when this weapon is fired.</summary>
        public int HeatSelf;
        /// <summary>Secondary burst radius for Annihilator-style splash (0 = none).</summary>
        public int SplashBurst;
        public LancerAttackEffectKind Effect = LancerAttackEffectKind.RifleFlash;
    }

    public sealed class LancerChassisDef
    {
        public required string Id;
        public required string NameLoc;
        public required string SpriteState;
        public int MaxHp = 14;
        public int Armor;
        public int Evasion = 8;
        public int Speed = 4;
        public int HeatCap = 6;
        public int RepairCap = 6;
        public int MaxStructure = 4;
        public LancerCoreKind CoreKind = LancerCoreKind.Raijin;
        public int OverwatchThreatRange = 1;
        /// <summary>
        /// Vanguard talent line (Handshake Etiquette / See-Through Seeker / Semper Vigilo).
        /// Tortuga pilots have the full series by default.
        /// </summary>
        public bool HasVanguard;
        /// <summary>Tortuga Sentinel trait: +1 ACC on reaction attacks (always on).</summary>
        public bool HasSentinel;
        /// <summary>
        /// Nuclear Cavalier (Ranks I–II): while in the Danger Zone (heat ≥ half cap),
        /// the first attack roll on your turn that hits deals +2 heat and +1d6 energy.
        /// </summary>
        public bool HasNuclearCavalier;
        /// <summary>External Batteries: +5 range / +1 threat on energy weapons; destroyed on structure damage.</summary>
        public bool HasExternalBatteries;
        /// <summary>Deep Well Heat Sink: start turn in Danger Zone → resistance to heat for the turn.</summary>
        public bool HasDeepWellHeatSink;
        public required string[] WeaponIds;
    }

    public sealed class LancerMissionLoadoutDef
    {
        public required string Id;
        public required string NameLoc;
        public required string DescriptionLoc;
        public required string ChassisId;
        /// <summary>Optional per-slot weapon overrides (null entries keep chassis default).</summary>
        public string?[]? WeaponOverrides;
        /// <summary>Optional sprite override; otherwise chassis SpriteState is used.</summary>
        public string? SpriteState;
    }

    public sealed class LancerEnemyTierStats
    {
        public int Hp;
        public int Armor;
        public int Evasion;
        public int AccBonus;
        public string WeaponId = WeaponGruntRifle;
    }

    public static readonly Dictionary<string, LancerWeaponDef> Weapons = BuildWeapons();
    public static readonly Dictionary<string, LancerChassisDef> Chassis = BuildChassis();
    public static readonly Dictionary<string, LancerMissionLoadoutDef> MissionLoadouts = BuildMissionLoadouts();

    public static readonly Dictionary<string, string[]> MissionLoadoutPairs = new()
    {
        ["tutorial"] = [LoadoutRaijinStrike],
        ["ridge-pass"] = [LoadoutRaijinStrike, LoadoutRaijinLicense],
        ["deep-range"] = [LoadoutTortuga, LoadoutTokugawa],
        ["crown-signal"] = [LoadoutTortuga, LoadoutTokugawa],
    };

    /// <summary>
    /// Per-mission sprite overrides for shared loadouts (e.g. deep-range Everest pistol skins
    /// vs crown-signal chassis defaults).
    /// </summary>
    public static readonly Dictionary<(string MissionId, string LoadoutId), string> MissionLoadoutSprites = new()
    {
        [("deep-range", LoadoutTortuga)] = "everest_blue_pistol",
        [("deep-range", LoadoutTokugawa)] = "everest_con_pistol",
    };

    public const string MissionTutorial = "tutorial";

    private static Dictionary<string, LancerMissionLoadoutDef> BuildMissionLoadouts()
    {
        return new Dictionary<string, LancerMissionLoadoutDef>
        {
            [LoadoutRaijinStrike] = new()
            {
                Id = LoadoutRaijinStrike,
                NameLoc = "lancer-loadout-raijin-strike-label",
                DescriptionLoc = "lancer-loadout-raijin-strike-desc",
                ChassisId = ChassisRaijin,
                SpriteState = "everest_blue",
            },
            [LoadoutRaijinLicense] = new()
            {
                Id = LoadoutRaijinLicense,
                NameLoc = "lancer-loadout-raijin-license-label",
                DescriptionLoc = "lancer-loadout-raijin-license-desc",
                ChassisId = ChassisRaijin,
                WeaponOverrides = [null, WeaponCannibal, null],
                SpriteState = "everest_red",
            },
            [LoadoutTortuga] = new()
            {
                Id = LoadoutTortuga,
                NameLoc = "lancer-loadout-tortuga-label",
                DescriptionLoc = "lancer-loadout-tortuga-desc",
                ChassisId = ChassisTortuga,
            },
            [LoadoutMonarch] = new()
            {
                Id = LoadoutMonarch,
                NameLoc = "lancer-loadout-monarch-label",
                DescriptionLoc = "lancer-loadout-monarch-desc",
                ChassisId = ChassisMonarch,
            },
            [LoadoutTokugawa] = new()
            {
                Id = LoadoutTokugawa,
                NameLoc = "lancer-loadout-tokugawa-label",
                DescriptionLoc = "lancer-loadout-tokugawa-desc",
                ChassisId = ChassisTokugawa,
            },
        };
    }
    private static Dictionary<string, LancerWeaponDef> BuildWeapons()
    {
        return new Dictionary<string, LancerWeaponDef>
        {
            [WeaponAmr] = new()
            {
                Id = WeaponAmr,
                NameLoc = "lancer-arcade-weapon-amr",
                Range = 15,
                DamageDice = 2,
                DamageSides = 6,
                Tags = LancerWeaponTags.Ordnance | LancerWeaponTags.Loading,
                Effect = LancerAttackEffectKind.AmrImpact,
            },
            [WeaponAssaultRifle] = new()
            {
                Id = WeaponAssaultRifle,
                NameLoc = "lancer-arcade-weapon-rifle",
                Range = 10,
                DamageDice = 1,
                DamageSides = 6,
                ReliableMiss = 2,
                Effect = LancerAttackEffectKind.RifleFlash,
            },
            [WeaponKnife] = new()
            {
                Id = WeaponKnife,
                NameLoc = "lancer-arcade-weapon-knife",
                Range = 1,
                DamageDice = 1,
                DamageSides = 3,
                DamageFlat = 1,
                Tags = LancerWeaponTags.Aux | LancerWeaponTags.Melee,
                Effect = LancerAttackEffectKind.KnifeSlash,
            },
            [WeaponSegmentKnife] = new()
            {
                Id = WeaponSegmentKnife,
                NameLoc = "lancer-arcade-weapon-segment-knife",
                Range = 1,
                DamageDice = 1,
                DamageSides = 3,
                DamageFlat = 1,
                Tags = LancerWeaponTags.Aux | LancerWeaponTags.Melee | LancerWeaponTags.Overkill,
                Effect = LancerAttackEffectKind.KnifeSlash,
            },
            [WeaponCannibal] = new()
            {
                Id = WeaponCannibal,
                NameLoc = "lancer-arcade-weapon-cannibal",
                Range = 5,
                DamageDice = 1,
                DamageSides = 6,
                DamageFlat = 2,
                AccWithin = 3,
                Tags = LancerWeaponTags.Cqb,
                Effect = LancerAttackEffectKind.RifleFlash,
            },
            [WeaponDeckSweeper] = new()
            {
                Id = WeaponDeckSweeper,
                NameLoc = "lancer-arcade-weapon-deck-sweeper",
                Range = 3,
                DamageDice = 2,
                DamageSides = 6,
                Tags = LancerWeaponTags.Cqb | LancerWeaponTags.Inaccurate,
                Effect = LancerAttackEffectKind.RifleFlash,
            },
            [WeaponCatalyticHammer] = new()
            {
                Id = WeaponCatalyticHammer,
                NameLoc = "lancer-arcade-weapon-catalytic-hammer",
                Range = 1,
                DamageDice = 1,
                DamageSides = 3,
                DamageFlat = 5,
                Tags = LancerWeaponTags.Melee | LancerWeaponTags.Loading | LancerWeaponTags.StunOnCrit,
                Effect = LancerAttackEffectKind.KnifeSlash,
            },
            [WeaponJavelin] = new()
            {
                Id = WeaponJavelin,
                NameLoc = "lancer-arcade-weapon-javelin",
                Range = 10,
                DamageDice = 1,
                DamageSides = 6,
                ReliableMiss = 1,
                Tags = LancerWeaponTags.Arcing,
                Effect = LancerAttackEffectKind.RocketImpact,
            },
            [WeaponPinaka] = new()
            {
                Id = WeaponPinaka,
                NameLoc = "lancer-arcade-weapon-pinaka",
                Range = 15,
                DamageDice = 2,
                DamageSides = 6,
                Tags = LancerWeaponTags.Ordnance | LancerWeaponTags.Loading | LancerWeaponTags.Arcing,
                BlastRadius = 1,
                Effect = LancerAttackEffectKind.MissileBlast,
            },
            [WeaponAnnihilator] = new()
            {
                Id = WeaponAnnihilator,
                NameLoc = "lancer-arcade-weapon-annihilator",
                Range = 5,
                DamageDice = 1,
                DamageSides = 3,
                DamageFlat = 2,
                Tags = LancerWeaponTags.Energy | LancerWeaponTags.Ap,
                HeatSelf = 2,
                SplashBurst = 1,
                Effect = LancerAttackEffectKind.AmrImpact,
            },
            [WeaponTorch] = new()
            {
                Id = WeaponTorch,
                NameLoc = "lancer-arcade-weapon-torch",
                Range = 1,
                DamageDice = 1,
                DamageSides = 6,
                DamageFlat = 3,
                Tags = LancerWeaponTags.Energy | LancerWeaponTags.Melee,
                HeatSelf = 2,
                Effect = LancerAttackEffectKind.KnifeSlash,
            },
            [WeaponGruntRifle] = new()
            {
                Id = WeaponGruntRifle,
                NameLoc = "lancer-arcade-weapon-grunt",
                Range = 10,
                DamageDice = 1,
                DamageSides = 6,
                ReliableMiss = 2,
                Effect = LancerAttackEffectKind.RifleFlash,
            },
            [WeaponCutlassRifle] = new()
            {
                Id = WeaponCutlassRifle,
                NameLoc = "lancer-arcade-weapon-cutlass",
                Range = 10,
                DamageDice = 1,
                DamageSides = 6,
                ReliableMiss = 2,
                Effect = LancerAttackEffectKind.RifleFlash,
            },
            // Core Assault Heavy Assault Rifle — Reliable Main Rifle R10 (arcade dice).
            [WeaponAssaultRifleNpc] = new()
            {
                Id = WeaponAssaultRifleNpc,
                NameLoc = "lancer-arcade-weapon-assault-npc",
                Range = 10,
                DamageDice = 1,
                DamageSides = 6,
                ReliableMiss = 2,
                Effect = LancerAttackEffectKind.RifleFlash,
            },
            // Core Sniper Anti-Materiel Rifle — Loading + Ordnance + AP (arcade-scaled: R15, 1d6+2).
            [WeaponSniperAmr] = new()
            {
                Id = WeaponSniperAmr,
                NameLoc = "lancer-arcade-weapon-sniper-amr",
                Range = 15,
                DamageDice = 1,
                DamageSides = 6,
                DamageFlat = 2,
                Tags = LancerWeaponTags.Ordnance | LancerWeaponTags.Loading | LancerWeaponTags.Ap,
                Effect = LancerAttackEffectKind.AmrImpact,
            },
            [WeaponBombard] = new()
            {
                Id = WeaponBombard,
                NameLoc = "lancer-arcade-weapon-bombard",
                Range = 12,
                DamageDice = 1,
                DamageSides = 6,
                DamageFlat = 2,
                Tags = LancerWeaponTags.Arcing | LancerWeaponTags.Loading,
                BlastRadius = 1,
                Effect = LancerAttackEffectKind.MissileBlast,
            },
        };
    }

    private static Dictionary<string, LancerChassisDef> BuildChassis()
    {
        return new Dictionary<string, LancerChassisDef>
        {
            [ChassisRaijin] = new()
            {
                Id = ChassisRaijin,
                NameLoc = "lancer-chassis-raijin-name",
                SpriteState = "raijin",
                MaxHp = 14,
                Evasion = 8,
                Speed = 4,
                HeatCap = 6,
                RepairCap = 6,
                MaxStructure = 4,
                CoreKind = LancerCoreKind.Raijin,
                WeaponIds = [WeaponAmr, WeaponAssaultRifle, WeaponKnife],
            },
            [ChassisTortuga] = new()
            {
                Id = ChassisTortuga,
                NameLoc = "lancer-chassis-tortuga-name",
                SpriteState = "tortuga",
                // Core Tortuga: HP 8, Armor 2, EVA 6, SPD 3, Heat Cap 6, Repair 6.
                MaxHp = 8,
                Armor = 2,
                Evasion = 6,
                Speed = 3,
                HeatCap = 6,
                RepairCap = 6,
                MaxStructure = 4,
                CoreKind = LancerCoreKind.TortugaSentinel,
                // Deck-Sweeper Threat 3; Hyper-Reflex also raises ranged Threat to 3.
                OverwatchThreatRange = 3,
                HasVanguard = true,
                HasSentinel = true,
                // Main CQB + Main Melee (Loading). Slot 2 unused.
                WeaponIds = [WeaponDeckSweeper, WeaponCatalyticHammer, ""],
            },
            [ChassisMonarch] = new()
            {
                Id = ChassisMonarch,
                NameLoc = "lancer-chassis-monarch-name",
                SpriteState = "monarch",
                MaxHp = 10,
                Evasion = 9,
                Speed = 4,
                HeatCap = 6,
                RepairCap = 6,
                MaxStructure = 4,
                CoreKind = LancerCoreKind.MonarchDivine,
                WeaponIds = [WeaponPinaka, WeaponJavelin, WeaponKnife],
            },
            [ChassisTokugawa] = new()
            {
                Id = ChassisTokugawa,
                NameLoc = "lancer-chassis-tokugawa-name",
                SpriteState = "tokugawa",
                MaxHp = 8,
                Armor = 1,
                Evasion = 8,
                Speed = 4,
                HeatCap = 8,
                RepairCap = 4,
                MaxStructure = 4,
                CoreKind = LancerCoreKind.TokugawaRadiance,
                HasNuclearCavalier = true,
                HasExternalBatteries = true,
                HasDeepWellHeatSink = true,
                // Aux/Aux mount: Segment Knife fires twice via StartAuxSkirmishSequence.
                WeaponIds = [WeaponAnnihilator, WeaponTorch, WeaponSegmentKnife],
            },
        };
    }

    /// <summary>Grunt-template chaff (urbie sprite).</summary>
    public static bool IsMookKind(LancerUnitKind kind) =>
        kind is LancerUnitKind.Grunt or LancerUnitKind.Urbie;

    /// <summary>Loading artillery that prefer range (Bombard mortar / Sniper AMR).</summary>
    public static bool IsArtilleryKind(LancerUnitKind kind) =>
        kind is LancerUnitKind.Bombard or LancerUnitKind.Sniper;

    public static bool IsEnemyKind(LancerUnitKind kind) =>
        kind is LancerUnitKind.Grunt or LancerUnitKind.Urbie or LancerUnitKind.Assault
            or LancerUnitKind.Cutlass or LancerUnitKind.Bombard or LancerUnitKind.Sniper;

    public static LancerEnemyTierStats GetEnemyStats(LancerUnitKind kind, int tier, bool veteran = false)
    {
        tier = Math.Clamp(tier, 0, 2);
        return kind switch
        {
            // Grunt-template Assault (Solo Microgame Raider) — 1 HP chaff, urbie sprite.
            LancerUnitKind.Grunt or LancerUnitKind.Urbie => tier switch
            {
                0 => new() { Hp = 1, Armor = 1, Evasion = 8, AccBonus = 0, WeaponId = WeaponGruntRifle },
                1 => new() { Hp = 2, Armor = 1, Evasion = 8, AccBonus = 0, WeaponId = WeaponGruntRifle },
                _ => new() { Hp = 3, Armor = 2, Evasion = 9, AccBonus = 1, WeaponId = WeaponGruntRifle },
            },
            // Core Assault striker — mid-tier between Grunt and elite Cutlass (kerberos_grunt).
            LancerUnitKind.Assault => tier switch
            {
                0 => new() { Hp = 6, Armor = 1, Evasion = 8, AccBonus = 1, WeaponId = WeaponAssaultRifleNpc },
                1 => new() { Hp = 8, Armor = 1, Evasion = 9, AccBonus = 1, WeaponId = WeaponAssaultRifleNpc },
                _ => new() { Hp = 10, Armor = 1, Evasion = 10, AccBonus = 2, WeaponId = WeaponAssaultRifleNpc },
            },
            // Elite Assault with Hunker (Cutlass) — full Assault HP band.
            LancerUnitKind.Cutlass => veteran
                ? new() { Hp = 22, Armor = 2, Evasion = 9, AccBonus = 2, WeaponId = WeaponCutlassRifle }
                : tier switch
                {
                    0 => new() { Hp = 15, Armor = 1, Evasion = 8, AccBonus = 1, WeaponId = WeaponCutlassRifle },
                    1 => new() { Hp = 18, Armor = 1, Evasion = 8, AccBonus = 1, WeaponId = WeaponCutlassRifle },
                    _ => new() { Hp = 20, Armor = 2, Evasion = 9, AccBonus = 2, WeaponId = WeaponCutlassRifle },
                },
            // Core Sniper — glass artillery, long-range Loading AMR (kerberos_sniper).
            // Tier 0 is a light scout-sniper suitable for early missions.
            LancerUnitKind.Sniper => tier switch
            {
                0 => new() { Hp = 5, Armor = 0, Evasion = 9, AccBonus = 1, WeaponId = WeaponSniperAmr },
                1 => new() { Hp = 9, Armor = 0, Evasion = 10, AccBonus = 2, WeaponId = WeaponSniperAmr },
                _ => new() { Hp = 12, Armor = 0, Evasion = 12, AccBonus = 3, WeaponId = WeaponSniperAmr },
            },
            LancerUnitKind.Bombard => tier switch
            {
                0 => new() { Hp = 8, Armor = 1, Evasion = 6, AccBonus = 0, WeaponId = WeaponBombard },
                1 => new() { Hp = 10, Armor = 1, Evasion = 6, AccBonus = 1, WeaponId = WeaponBombard },
                _ => new() { Hp = 12, Armor = 2, Evasion = 7, AccBonus = 1, WeaponId = WeaponBombard },
            },
            _ => new() { Hp = 1, Armor = 0, Evasion = 8, AccBonus = 0, WeaponId = WeaponGruntRifle },
        };
    }

    public static string GetUnitSprite(LancerUnitKind kind, string playerSprite = "everest_blue") =>
        kind switch
        {
            LancerUnitKind.PlayerMech => playerSprite,
            LancerUnitKind.Grunt => "urbie",
            LancerUnitKind.Urbie => "urbie",
            LancerUnitKind.Assault => "kerberos_grunt",
            LancerUnitKind.Cutlass => "kerberos_archer",
            LancerUnitKind.Sniper => "kerberos_sniper",
            LancerUnitKind.Bombard => "kerberos_bombard",
            _ => string.Empty,
        };
}
