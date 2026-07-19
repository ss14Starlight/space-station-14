using System.Linq;
using Content.Shared._Starlight.Arcade.Lancer;
using Robust.Shared.Prototypes;
using static Content.Server._Starlight.Arcade.Lancer.LancerGame;

namespace Content.Server._Starlight.Arcade.Lancer;

/// <summary>
/// Headless Monte Carlo combat evaluator for Lancer arcade balance.
/// Ignores mission objectives — fights are eliminate-all. Relay destruction is a loss.
/// Balance is measured against a competent scripted player policy, not optimal play.
/// Non-arcing attacks use the same rubble-hard LOS check as the live game.
/// Still optimistic vs live: no full Stress/overheat/structure-break fidelity.
/// </summary>
public sealed class LancerCombatSimulator
{
    private readonly IPrototypeManager _prototypes;
    private readonly System.Random _rng;
    /// <summary>
    /// Earliest player turn (1-based) the Tokugawa bot may activate Radiance.
    /// 1 = as soon as useful; higher = wait for grunts to die first.
    /// </summary>
    private int _radianceMinTurn = 3;

    public LancerCombatSimulator(IPrototypeManager prototypes, int? seed = null)
    {
        _prototypes = prototypes;
        _rng = seed is { } s ? new System.Random(s) : new System.Random();
    }

    public void SetRadianceMinTurn(int turn) => _radianceMinTurn = Math.Max(1, turn);

    public sealed class BalanceResult
    {
        public required string MissionId;
        public required string LoadoutId;
        public int Hull;
        public int Agility;
        public int Engineering;
        public int Trials;
        public int Wins;
        public double WinRate => Trials <= 0 ? 0 : (double) Wins / Trials;
    }

    public (int Wins, int Died, int Timeout, int RelayLost, int DamageDealt) DiagnoseMission(
        string missionId,
        string loadoutId,
        int trials,
        int hull = 2,
        int agility = 0,
        int engineering = 0)
    {
        if (!_prototypes.TryIndex(missionId, out LancerMissionPrototype? mission))
            throw new InvalidOperationException($"Unknown mission '{missionId}'");

        var wins = 0;
        var died = 0;
        var timeout = 0;
        var relay = 0;
        var damage = 0;

        for (var i = 0; i < trials; i++)
        {
            var (ok, reason, dmg) = SimulateMissionDiagnose(mission, loadoutId, hull, agility, engineering);
            damage += dmg;
            if (ok)
                wins++;
            else if (reason == "died")
                died++;
            else if (reason == "timeout")
                timeout++;
            else if (reason == "relay")
                relay++;
        }

        return (wins, died, timeout, relay, damage);
    }

    private (bool Ok, string Reason, int DamageDealt) SimulateMissionDiagnose(
        LancerMissionPrototype mission,
        string loadoutId,
        int hull,
        int agility,
        int engineering)
    {
        if (!LancerGame.MissionLoadouts.TryGetValue(loadoutId, out var loadout))
            return (false, "noloadout", 0);

        var player = BuildPlayer(loadout, hull, agility, engineering);
        var damageDealt = 0;

        var encounterIds = mission.LoadoutEncounters.TryGetValue(loadoutId, out var ov) && ov.Count > 0
            ? ov : mission.Encounters;
        foreach (var encounterId in encounterIds)
        {
            if (!_prototypes.TryIndex(encounterId, out LancerEncounterPrototype? encounter))
                return (false, "noenc", damageDealt);

            player.Heat = player.HasNuclearCavalier
                ? Math.Max(player.Heat, (player.HeatCap + 1) / 2)
                : 0;
            player.HexCharges = player.HexCap;
            player.CoreActive = false;
            player.CorePower = 1;
            player.Exposed = false;
            player.ExposedTurnsRemaining = 0;
            player.NuclearCavalierUsedThisTurn = false;
            player.PlayerTurnIndex = 0;
            player.RadianceMinTurn = _radianceMinTurn;
            player.ExternalBatteriesDestroyed = false;
            player.DeepWellHeatResistThisTurn = false;
            ResetWeapons(player);

            var (ok, reason, dmg) = SimulateFightDiagnose(encounter, player);
            damageDealt += dmg;
            if (!ok)
                return (false, reason, damageDealt);

            while (player.Repairs > 0 && player.Structure < player.MaxStructure)
            {
                player.Repairs--;
                player.Structure++;
            }

            while (player.Repairs > 0 && player.Hp < player.MaxHp)
            {
                player.Repairs--;
                player.Hp = player.MaxHp;
            }
        }

        return (true, "win", damageDealt);
    }

    private (bool Ok, string Reason, int DamageDealt) SimulateFightDiagnose(
        LancerEncounterPrototype encounter,
        SimPlayer player)
    {
        var terrain = new LancerTerrainType[GridSize, GridSize];
        foreach (var entry in encounter.Terrains)
        {
            if (entry.X is >= 0 and < GridSize && entry.Y is >= 0 and < GridSize)
                terrain[entry.X, entry.Y] = entry.Terrain;
        }

        SimUnit? relay = null;
        if (encounter.HasRelay)
        {
            terrain[encounter.RelayX, encounter.RelayY] = LancerTerrainType.Relay;
            relay = new SimUnit
            {
                Kind = LancerUnitKind.Relay,
                X = encounter.RelayX,
                Y = encounter.RelayY,
                Hp = RelayMaxHp,
                MaxHp = RelayMaxHp,
                Armor = 0,
                Evasion = RelayEvasion,
            };
        }

        var enemies = new List<SimUnit>();
        foreach (var spawn in encounter.Enemies)
        {
            var stats = GetEnemyStats(spawn.Kind, spawn.Tier, spawn.Veteran);
            enemies.Add(new SimUnit
            {
                Kind = spawn.Kind,
                X = spawn.X,
                Y = spawn.Y,
                Hp = stats.Hp,
                MaxHp = stats.Hp,
                Armor = stats.Armor,
                Evasion = stats.Evasion,
                AccBonus = stats.AccBonus,
                WeaponId = stats.WeaponId,
            });
        }

        var startingHp = enemies.Sum(e => e.Hp);
        var px = encounter.PlayerDeployX;
        var py = encounter.PlayerDeployY;
        const int maxRounds = 40;

        for (var round = 0; round < maxRounds; round++)
        {
            PlayerTurn(player, ref px, ref py, enemies, relay, terrain);
            if (player.Structure <= 0)
                return (false, "died", startingHp - enemies.Sum(e => Math.Max(0, e.Hp)));
            if (relay is { Destroyed: true })
                return (false, "relay", startingHp - enemies.Sum(e => Math.Max(0, e.Hp)));
            if (enemies.All(e => e.Destroyed || e.Fleeing))
                return (true, "win", startingHp);

            foreach (var enemy in enemies)
            {
                if (enemy.Destroyed || enemy.Fleeing)
                    continue;

                EnemyTurn(enemy, player, ref px, ref py, enemies, relay, terrain);
                if (player.Structure <= 0)
                    return (false, "died", startingHp - enemies.Sum(e => Math.Max(0, e.Hp)));
                if (relay is { Destroyed: true })
                    return (false, "relay", startingHp - enemies.Sum(e => Math.Max(0, e.Hp)));
            }

            if (enemies.All(e => e.Destroyed || e.Fleeing))
                return (true, "win", startingHp);
        }

        return (false, "timeout", startingHp - enemies.Sum(e => Math.Max(0, e.Hp)));
    }

    public BalanceResult EvaluateMission(
        string missionId,
        string loadoutId,
        int trials,
        int hull = 2,
        int agility = 0,
        int engineering = 0,
        bool disableTokugawaCore = false)
    {
        if (!_prototypes.TryIndex(missionId, out LancerMissionPrototype? mission))
            throw new InvalidOperationException($"Unknown mission '{missionId}'");

        var wins = 0;
        for (var i = 0; i < trials; i++)
        {
            if (SimulateMission(mission, loadoutId, hull, agility, engineering, disableTokugawaCore))
                wins++;
        }

        return new BalanceResult
        {
            MissionId = missionId,
            LoadoutId = loadoutId,
            Hull = hull,
            Agility = agility,
            Engineering = engineering,
            Trials = trials,
            Wins = wins
        };
    }

    private bool SimulateMission(
        LancerMissionPrototype mission,
        string loadoutId,
        int hull,
        int agility,
        int engineering,
        bool disableTokugawaCore = false)
    {
        if (!LancerGame.MissionLoadouts.TryGetValue(loadoutId, out var loadout))
            return false;

        var player = BuildPlayer(loadout, hull, agility, engineering);
        if (disableTokugawaCore && player.CoreKind == LancerCoreKind.TokugawaRadiance)
            player.CorePower = 0;

        var encounterIds = mission.LoadoutEncounters.TryGetValue(loadoutId, out var ov) && ov.Count > 0
            ? ov : mission.Encounters;
        foreach (var encounterId in encounterIds)
        {
            if (!_prototypes.TryIndex(encounterId, out LancerEncounterPrototype? encounter))
                return false;

            // Carry HP/structure/repairs between fights; refresh heat/hex each fight.
            player.Heat = player.HasNuclearCavalier
                ? Math.Max(player.Heat, (player.HeatCap + 1) / 2) // start in Danger Zone
                : 0;
            player.HexCharges = player.HexCap;
            player.CoreActive = false;
            player.CorePower = disableTokugawaCore ? 0 : 1;
            player.Exposed = false;
            player.ExposedTurnsRemaining = 0;
            player.NuclearCavalierUsedThisTurn = false;
            player.PlayerTurnIndex = 0;
            player.RadianceMinTurn = _radianceMinTurn;
            player.ExternalBatteriesDestroyed = false;
            player.DeepWellHeatResistThisTurn = false;
            ResetWeapons(player);

            if (!SimulateFight(encounter, player))
                return false;

            // Simple intermission: restore structure then reactor while repairs remain.
            while (player.Repairs > 0 && player.Structure < player.MaxStructure)
            {
                player.Repairs--;
                player.Structure++;
            }

            while (player.Repairs > 0 && player.Hp < player.MaxHp)
            {
                player.Repairs--;
                player.Hp = player.MaxHp;
            }
        }

        return true;
    }

    private SimPlayer BuildPlayer(LancerMissionLoadoutDef loadout, int hull, int agility, int engineering)
    {
        if (!LancerGame.Chassis.TryGetValue(loadout.ChassisId, out var chassis))
            chassis = LancerGame.Chassis[ChassisRaijin];

        var weapons = new string[WeaponSlotCount];
        for (var i = 0; i < WeaponSlotCount; i++)
            weapons[i] = i < chassis.WeaponIds.Length ? chassis.WeaponIds[i] : string.Empty;

        if (loadout.WeaponOverrides is { } overrides)
        {
            for (var i = 0; i < WeaponSlotCount && i < overrides.Length; i++)
            {
                if (!string.IsNullOrEmpty(overrides[i]))
                    weapons[i] = overrides[i]!;
            }
        }

        var maxHp = chassis.MaxHp + hull * 2;
        var speed = chassis.Speed + agility;
        var evasion = chassis.Evasion + agility / 2;
        var heatCap = chassis.HeatCap + engineering;
        var hexCap = PlayerHexCharges + engineering / 2;

        return new SimPlayer
        {
            MaxHp = maxHp,
            Hp = maxHp,
            MaxStructure = chassis.MaxStructure,
            Structure = chassis.MaxStructure,
            Armor = chassis.Armor,
            Evasion = evasion,
            Speed = speed,
            HeatCap = heatCap,
            Heat = 0,
            RepairCap = chassis.RepairCap,
            Repairs = chassis.RepairCap,
            HexCap = hexCap,
            HexCharges = hexCap,
            OverwatchRange = chassis.OverwatchThreatRange,
            HasVanguard = chassis.HasVanguard,
            HasSentinel = chassis.HasSentinel,
            CoreKind = chassis.CoreKind,
            HasNuclearCavalier = chassis.HasNuclearCavalier,
            HasExternalBatteries = chassis.HasExternalBatteries,
            HasDeepWellHeatSink = chassis.HasDeepWellHeatSink,
            CorePower = 1,
            WeaponIds = weapons,
            WeaponLoaded = [true, true, true],
        };
    }

    private static void ResetWeapons(SimPlayer player)
    {
        // Loading weapons start loaded (match live game); empty slots stay unloaded.
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            var id = player.WeaponIds[i];
            if (string.IsNullOrEmpty(id) || !LancerGame.Weapons.ContainsKey(id))
            {
                player.WeaponLoaded[i] = false;
                continue;
            }

            player.WeaponLoaded[i] = true;
        }
    }

    private bool SimulateFight(LancerEncounterPrototype encounter, SimPlayer player)
    {
        var terrain = new LancerTerrainType[GridSize, GridSize];
        foreach (var entry in encounter.Terrains)
        {
            if (entry.X is >= 0 and < GridSize && entry.Y is >= 0 and < GridSize)
                terrain[entry.X, entry.Y] = entry.Terrain;
        }

        SimUnit? relay = null;
        if (encounter.HasRelay)
        {
            terrain[encounter.RelayX, encounter.RelayY] = LancerTerrainType.Relay;
            relay = new SimUnit
            {
                Kind = LancerUnitKind.Relay,
                X = encounter.RelayX,
                Y = encounter.RelayY,
                Hp = RelayMaxHp,
                MaxHp = RelayMaxHp,
                Armor = 0,
                Evasion = RelayEvasion,
            };
        }

        var enemies = new List<SimUnit>();
        foreach (var spawn in encounter.Enemies)
        {
            var stats = GetEnemyStats(spawn.Kind, spawn.Tier, spawn.Veteran);
            enemies.Add(new SimUnit
            {
                Kind = spawn.Kind,
                X = spawn.X,
                Y = spawn.Y,
                Hp = stats.Hp,
                MaxHp = stats.Hp,
                Armor = stats.Armor,
                Evasion = stats.Evasion,
                AccBonus = stats.AccBonus,
                WeaponId = stats.WeaponId,
            });
        }

        var px = encounter.PlayerDeployX;
        var py = encounter.PlayerDeployY;
        const int maxRounds = 40;

        for (var round = 0; round < maxRounds; round++)
        {
            // Player turn
            PlayerTurn(player, ref px, ref py, enemies, relay, terrain);
            if (player.Structure <= 0)
                return false;
            if (relay is { Destroyed: true })
                return false;
            if (enemies.All(e => e.Destroyed || e.Fleeing))
                return true;

            // Enemy turn
            foreach (var enemy in enemies)
            {
                if (enemy.Destroyed || enemy.Fleeing)
                    continue;

                EnemyTurn(enemy, player, ref px, ref py, enemies, relay, terrain);
                if (player.Structure <= 0)
                    return false;
                if (relay is { Destroyed: true })
                    return false;
            }

            if (enemies.All(e => e.Destroyed || e.Fleeing))
                return true;
        }

        return false;
    }

    private void PlayerTurn(
        SimPlayer player,
        ref int px,
        ref int py,
        List<SimUnit> enemies,
        SimUnit? relay,
        LancerTerrainType[,] terrain)
    {
        TickExposed(player);
        player.NuclearCavalierUsedThisTurn = false;
        player.DeepWellHeatResistThisTurn = false;
        player.OverwatchUsesThisRound = 0;
        player.BraceUsedThisRound = false;
        player.PlayerTurnIndex++;

        foreach (var enemy in enemies)
            enemy.Hunkered = false;

        if (player.HasDeepWellHeatSink && IsInDangerZone(player))
            player.DeepWellHeatResistThisTurn = true;

        var alive = enemies.Where(e => !e.Destroyed && !e.Fleeing).ToList();
        if (alive.Count == 0)
            return;

        // Stabilize if critically damaged.
        if (player.Structure <= 1 && player.Repairs > 0 && player.Hp < player.MaxHp / 2)
        {
            player.Repairs--;
            player.Hp = player.MaxHp;
            return;
        }

        // Nuclear Cavalier wants Danger Zone; only vent when overheat is imminent.
        if (player.HasNuclearCavalier)
        {
            if (player.Heat >= player.HeatCap)
                player.Heat = 0;
        }
        else if (player.Heat >= player.HeatCap - 2)
        {
            player.Heat = 0;
        }

        // Reload loading weapons when nothing useful is ready.
        if (NeedsReload(player))
        {
            for (var i = 0; i < WeaponSlotCount; i++)
            {
                if (LancerGame.Weapons.TryGetValue(player.WeaponIds[i], out var def)
                    && def.Tags.HasFlag(LancerWeaponTags.Loading))
                {
                    player.WeaponLoaded[i] = true;
                }
            }

            return;
        }

        // HEX only when no ready Annihilator kill exists, or 3+ enemies are clustered.
        if (player.HexCharges > 0)
        {
            var cluster = FindHexCluster(alive);
            if (cluster != null)
            {
                var clusterSize = CountInHexBlast(alive, cluster.Value.X, cluster.Value.Y);
                var hasRangedKill = HasReadyAnnihilatorKill(player, px, py, alive);
                if (!hasRangedKill || clusterSize >= 3)
                {
                    player.HexCharges--;
                    ApplyHex(cluster.Value.X, cluster.Value.Y, enemies);
                    return;
                }
            }
        }

        var playerX = px;
        var playerY = py;
        var elitesAlive = alive.Count(e => !IsMookKind(e.Kind));
        const int annihilatorExpectedDmg = 4; // ~1d3+2 AP
        // Prefer elites when 2+ alive; only prioritize mooks when ≤1 elite
        // remains OR the mook is in Annihilator one-shot range.
        var target = alive
            .OrderBy(e =>
            {
                if (IsMookKind(e.Kind))
                {
                    if (elitesAlive <= 1 || e.Hp <= annihilatorExpectedDmg)
                        return 0;
                    return 2;
                }

                // Snipers are high priority (structure threat / long range).
                if (e.Kind == LancerUnitKind.Sniper)
                    return 0;
                return 1; // other elites
            })
            .ThenBy(e => e.Hp)
            .ThenBy(e => HexDist(playerX, playerY, e.X, e.Y))
            .First();

        MaybeActivateTokugawaRadiance(player, alive, px, py);
        MaybeActivateTortugaSentinel(player, alive);

        var maxRanged = GetMaxReadyRanged(player);
        var distToTarget = HexDist(px, py, target.X, target.Y);

        if (player.CoreKind == LancerCoreKind.TokugawaRadiance && maxRanged > 1)
        {
            // Glass cannon: close until Annihilator can fire, then hold / soft cover.
            if (distToTarget > maxRanged)
                StepToward(ref px, ref py, target.X, target.Y, player.Speed, terrain, enemies, relay, avoidHard: true);
            else if (distToTarget <= 2)
                StepAway(ref px, ref py, target.X, target.Y, player.Speed, terrain, enemies, relay);
            else
                SeekSoftCover(ref px, ref py, terrain, enemies, relay);
        }
        else
        {
            StepToward(ref px, ref py, target.X, target.Y, player.Speed, terrain, enemies, relay, avoidHard: true);
        }

        // Attack with best available weapon in range (2 quick actions ≈ 2 skirmishes).
        for (var shot = 0; shot < 2; shot++)
        {
            // Without Nuclear Cavalier, avoid dumping heat into a second self-heating shot.
            // With Deep Well, heat is halved — allow second shot unless it would exceed HeatCap.
            if (shot > 0)
            {
                if (player.HasNuclearCavalier)
                {
                    var heatSelf = EstimateNextShotHeatSelf(player, px, py, target);
                    var effectiveHeat = player.DeepWellHeatResistThisTurn
                        ? Math.Max(1, heatSelf / 2)
                        : heatSelf;
                    if (player.Heat + effectiveHeat > player.HeatCap)
                        break;
                }
                else if (player.Heat >= player.HeatCap - 2)
                {
                    break;
                }
            }

            var weaponIndex = PickBestWeapon(player, px, py, target, terrain);
            if (weaponIndex < 0)
            {
                if (shot == 0 && HexDist(px, py, target.X, target.Y) <= 1)
                    weaponIndex = 2;
                else
                    break;
            }

            ResolvePlayerAttack(player, weaponIndex, px, py, target, enemies, terrain);

            // Aux/Aux: Segment Knife (and other Aux slot-2 weapons) fire twice.
            if (weaponIndex == 2
                && LancerGame.Weapons.TryGetValue(player.WeaponIds[2], out var auxDef)
                && auxDef.Tags.HasFlag(LancerWeaponTags.Aux)
                && !target.Destroyed && !target.Fleeing)
            {
                ResolvePlayerAttack(player, 2, px, py, target, enemies, terrain);
            }

            if (target.Destroyed || target.Fleeing)
            {
                var remaining = enemies.Where(e => !e.Destroyed && !e.Fleeing).ToList();
                if (remaining.Count == 0)
                    return;
                var curX = px;
                var curY = py;
                var elitesLeft = remaining.Count(e => !IsMookKind(e.Kind));
                target = remaining
                    .OrderBy(e =>
                    {
                        if (IsMookKind(e.Kind))
                        {
                            if (elitesLeft <= 1 || e.Hp <= annihilatorExpectedDmg)
                                return 0;
                            return 2;
                        }

                        if (e.Kind == LancerUnitKind.Sniper)
                            return 0;
                        return 1;
                    })
                    .ThenBy(e => e.Hp)
                    .ThenBy(e => HexDist(curX, curY, e.X, e.Y))
                    .First();
            }
        }
    }

    /// <summary>
    /// Tortuga Sentinel: activate when 2+ hostiles are alive so the second Overwatch can fire.
    /// </summary>
    private static void MaybeActivateTortugaSentinel(SimPlayer player, List<SimUnit> alive)
    {
        if (player.CoreKind != LancerCoreKind.TortugaSentinel
            || player.CorePower <= 0
            || player.CoreActive)
            return;

        if (alive.Count < 2)
            return;

        player.CorePower--;
        player.CoreActive = true;
    }

    /// <summary>
    /// Prefer Radiance without Exposed for range. Never Exposed unless cutlassesAlive &lt;= 1 and structure &gt;= 3.
    /// May activate on turn 2 when needRange (even before RadianceMinTurn).
    /// </summary>
    private static void MaybeActivateTokugawaRadiance(SimPlayer player, List<SimUnit> alive, int px, int py)
    {
        if (player.CoreKind != LancerCoreKind.TokugawaRadiance
            || player.CorePower <= 0
            || player.CoreActive)
            return;

        var nearest = alive.Min(e => HexDist(px, py, e.X, e.Y));
        var needRange = nearest > 5; // base Annihilator can't reach
        var cutlassesAlive = alive.Count(e => e.Kind == LancerUnitKind.Cutlass);
        var totalEnemies = alive.Count;
        var gruntsAlive = alive.Count(e => IsMookKind(e.Kind));
        var fieldThinned = gruntsAlive <= 1 || totalEnemies <= 3;

        // Early activate (turn 2+) for range without Exposed; otherwise wait for RadianceMinTurn.
        var earlyRange = needRange && player.PlayerTurnIndex >= 2;
        if (!earlyRange && player.PlayerTurnIndex < player.RadianceMinTurn)
            return;

        var shouldActivate = earlyRange
            || needRange
            || fieldThinned
            || player.PlayerTurnIndex >= player.RadianceMinTurn + 2;
        if (!shouldActivate)
            return;

        player.CorePower--;
        player.CoreActive = true;

        // NEVER Exposed unless cutlasses thinned and structure is healthy.
        if (cutlassesAlive <= 1 && player.Structure >= 3)
        {
            player.Exposed = true;
            player.ExposedTurnsRemaining = 2;
        }
    }

    private static bool HasReadyAnnihilatorKill(SimPlayer player, int px, int py, List<SimUnit> alive)
    {
        const int expectedDmg = 4; // ~1d3+2
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            if (!player.WeaponLoaded[i])
                continue;
            if (!LancerGame.Weapons.TryGetValue(player.WeaponIds[i], out var def))
                continue;
            if (def.Id != WeaponAnnihilator && !def.Tags.HasFlag(LancerWeaponTags.Ap))
                continue;

            var range = GetEffectiveRange(player, def);
            foreach (var e in alive)
            {
                if (HexDist(px, py, e.X, e.Y) <= range && e.Hp <= expectedDmg)
                    return true;
            }
        }

        return false;
    }

    private static int CountInHexBlast(List<SimUnit> alive, int cx, int cy)
    {
        var n = 0;
        foreach (var e in alive)
        {
            if (HexDist(cx, cy, e.X, e.Y) <= 1)
                n++;
        }

        return n;
    }

    private static int EstimateNextShotHeatSelf(SimPlayer player, int px, int py, SimUnit target)
    {
        var weaponIndex = -1;
        var dist = HexDist(px, py, target.X, target.Y);
        var bestScore = int.MinValue;
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            if (!LancerGame.Weapons.TryGetValue(player.WeaponIds[i], out var def))
                continue;
            if (!player.WeaponLoaded[i])
                continue;
            if (dist > GetEffectiveRange(player, def))
                continue;

            var score = def.DamageDice * (def.DamageSides + 1) / 2 + def.DamageFlat;
            if (score > bestScore)
            {
                bestScore = score;
                weaponIndex = i;
            }
        }

        if (weaponIndex < 0)
            return 2; // conservative Annihilator heat

        return LancerGame.Weapons.TryGetValue(player.WeaponIds[weaponIndex], out var w) ? w.HeatSelf : 2;
    }

    private static int GetMaxReadyRanged(SimPlayer player)
    {
        var max = 0;
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            if (!player.WeaponLoaded[i])
                continue;
            if (!LancerGame.Weapons.TryGetValue(player.WeaponIds[i], out var def))
                continue;
            var range = GetEffectiveRange(player, def);
            if (range > max)
                max = range;
        }

        return max;
    }

    private static bool NeedsReload(SimPlayer player)
    {
        var hasReadyRanged = false;
        var hasEmptyLoading = false;
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            var id = player.WeaponIds[i];
            if (string.IsNullOrEmpty(id) || !LancerGame.Weapons.TryGetValue(id, out var def))
                continue;

            if (def.Tags.HasFlag(LancerWeaponTags.Loading) && !player.WeaponLoaded[i])
                hasEmptyLoading = true;

            // Include Radiance-extended energy weapons as ready ranged.
            if (GetEffectiveRange(player, def) > 1 && player.WeaponLoaded[i])
                hasReadyRanged = true;
        }

        return hasEmptyLoading && !hasReadyRanged;
    }

    private void EnemyTurn(
        SimUnit enemy,
        SimPlayer player,
        ref int px,
        ref int py,
        List<SimUnit> enemies,
        SimUnit? relay,
        LancerTerrainType[,] terrain)
    {
        if (!LancerGame.Weapons.TryGetValue(enemy.WeaponId, out var weapon))
            return;

        // Mooks prefer the relay; others prefer the player.
        int tx, ty;
        if (IsMookKind(enemy.Kind) && relay is { Destroyed: false })
        {
            tx = relay.X;
            ty = relay.Y;
        }
        else
        {
            tx = px;
            ty = py;
        }

        var startX = enemy.X;
        var startY = enemy.Y;
        var distBefore = HexDist(startX, startY, px, py);

        // Artillery hold range; others close in slowly (sim fidelity).
        var distToPlayer = distBefore;
        if (IsArtilleryKind(enemy.Kind) && !enemy.WeaponLoaded)
        {
            enemy.WeaponLoaded = true;
            return;
        }

        if (enemy.Kind == LancerUnitKind.Bombard)
        {
            var bombardSpeed = distToPlayer > weapon.Range ? 2 : 1;
            if (distToPlayer > weapon.Range)
                StepToward(ref enemy.X, ref enemy.Y, px, py, bombardSpeed, terrain, enemies, relay, avoidHard: false, self: enemy);
            else if (distToPlayer < 4)
                StepToward(ref enemy.X, ref enemy.Y, enemy.X + (enemy.X - px), enemy.Y + (enemy.Y - py), bombardSpeed, terrain, enemies, relay, avoidHard: false, self: enemy);
        }
        else if (enemy.Kind == LancerUnitKind.Sniper)
        {
            if (distToPlayer > weapon.Range)
                StepToward(ref enemy.X, ref enemy.Y, px, py, 1, terrain, enemies, relay, avoidHard: false, self: enemy);
            else if (distToPlayer < 8)
                StepToward(ref enemy.X, ref enemy.Y, enemy.X + (enemy.X - px), enemy.Y + (enemy.Y - py), 1, terrain, enemies, relay, avoidHard: false, self: enemy);
        }
        else
        {
            var speed = 1;
            StepToward(ref enemy.X, ref enemy.Y, tx, ty, speed, terrain, enemies, relay, avoidHard: false, self: enemy);
        }

        var moved = enemy.X != startX || enemy.Y != startY;
        TryPlayerOverwatch(player, enemy, px, py, enemies, terrain, distBefore, moved);
        if (enemy.Destroyed || enemy.Fleeing || player.Structure <= 0)
            return;

        if (weapon.Tags.HasFlag(LancerWeaponTags.Loading) && !enemy.WeaponLoaded)
            return;

        // Attack player if in range, else relay for grunts.
        if (HexDist(enemy.X, enemy.Y, px, py) <= weapon.Range
            && (weapon.Tags.HasFlag(LancerWeaponTags.Arcing)
                || HasSimLineOfSight(enemy.X, enemy.Y, px, py, terrain)))
        {
            ResolveEnemyAttack(enemy, weapon, px, py, player, terrain);
            return;
        }

        if (IsMookKind(enemy.Kind) && relay is { Destroyed: false }
            && HexDist(enemy.X, enemy.Y, relay.X, relay.Y) <= weapon.Range
            && (weapon.Tags.HasFlag(LancerWeaponTags.Arcing)
                || HasSimLineOfSight(enemy.X, enemy.Y, relay.X, relay.Y, terrain)))
        {
            ResolveEnemyAttackOnRelay(enemy, weapon, relay, terrain);
        }
    }

    private void TryPlayerOverwatch(
        SimPlayer player,
        SimUnit enemy,
        int px,
        int py,
        List<SimUnit> enemies,
        LancerTerrainType[,] terrain,
        int distBefore,
        bool moved)
    {
        // Brace lockout blocks reactions. Hyper-Reflex (core) allows 2 Overwatches/round.
        if (player.BraceUsedThisRound)
            return;
        var maxOverwatch = player.CoreKind == LancerCoreKind.TortugaSentinel && player.CoreActive ? 2 : 1;
        if (player.OverwatchUsesThisRound >= maxOverwatch)
            return;
        if (player.OverwatchRange <= 1)
            return;
        if (player.CoreKind != LancerCoreKind.TortugaSentinel && player.OverwatchRange < 3)
            return;
        if (enemy.Destroyed || enemy.Fleeing || !moved)
            return;

        var distAfter = HexDist(enemy.X, enemy.Y, px, py);
        var wasThreat = distBefore <= player.OverwatchRange;
        var entersThreat = distAfter <= player.OverwatchRange;
        // Classic: start inside threat. Vanguard III Semper Vigilo: also enter threat.
        if (!wasThreat && !(player.HasVanguard && entersThreat))
            return;

        var weaponIndex = PickOverwatchWeapon(player);
        if (weaponIndex < 0)
            return;

        player.OverwatchUsesThisRound++;
        ResolvePlayerAttack(player, weaponIndex, px, py, enemy, enemies, terrain);
    }

    private static int PickOverwatchWeapon(SimPlayer player)
    {
        // Prefer CQB / ranged; else best loaded weapon.
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            if (!player.WeaponLoaded[i])
                continue;
            if (!LancerGame.Weapons.TryGetValue(player.WeaponIds[i], out var def))
                continue;
            if (def.Tags.HasFlag(LancerWeaponTags.Cqb) || def.Range > 1)
                return i;
        }

        var best = -1;
        var bestScore = int.MinValue;
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            if (!player.WeaponLoaded[i])
                continue;
            if (!LancerGame.Weapons.TryGetValue(player.WeaponIds[i], out var def))
                continue;

            var score = def.DamageDice * (def.DamageSides + 1) / 2 + def.DamageFlat;
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private int PickBestWeapon(SimPlayer player, int px, int py, SimUnit target, LancerTerrainType[,] terrain)
    {
        var dist = HexDist(px, py, target.X, target.Y);
        var best = -1;
        var bestScore = int.MinValue;

        for (var i = 0; i < WeaponSlotCount; i++)
        {
            var id = player.WeaponIds[i];
            if (string.IsNullOrEmpty(id) || !LancerGame.Weapons.TryGetValue(id, out var def))
                continue;
            if (!player.WeaponLoaded[i])
                continue;
            if (dist > GetEffectiveRange(player, def))
                continue;
            if (!def.Tags.HasFlag(LancerWeaponTags.Arcing)
                && !HasSimLineOfSight(px, py, target.X, target.Y, terrain))
                continue;

            // Prefer higher expected damage; slight preference for sustained (non-loading) weapons.
            var score = def.DamageDice * (def.DamageSides + 1) / 2 + def.DamageFlat;
            score += GetTokugawaBonusDamage(player);
            if (def.Tags.HasFlag(LancerWeaponTags.Ordnance))
                score += 2;
            if (!def.Tags.HasFlag(LancerWeaponTags.Loading))
                score += 2;
            if (def.Tags.HasFlag(LancerWeaponTags.Ap))
                score += 2;
            if (def.Tags.HasFlag(LancerWeaponTags.Cqb) && player.HasVanguard && dist <= VanguardCqbRange)
                score += 2;
            if (def.AccWithin > 0 && dist <= def.AccWithin)
                score += 1;

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    private void ResolvePlayerAttack(
        SimPlayer player,
        int weaponIndex,
        int px,
        int py,
        SimUnit target,
        List<SimUnit> enemies,
        LancerTerrainType[,] terrain)
    {
        if (!LancerGame.Weapons.TryGetValue(player.WeaponIds[weaponIndex], out var weapon))
            return;

        var dist = HexDist(px, py, target.X, target.Y);
        if (dist > GetEffectiveRange(player, weapon))
            return;
        if (!weapon.Tags.HasFlag(LancerWeaponTags.Arcing)
            && !HasSimLineOfSight(px, py, target.X, target.Y, terrain))
            return;

        var isMelee = weapon.Tags.HasFlag(LancerWeaponTags.Melee) || weapon.Range <= 1;
        var acc = player.CoreKind == LancerCoreKind.Raijin && player.CoreActive ? 1 : 0;
        if (weapon.AccWithin > 0 && dist <= weapon.AccWithin)
            acc += 1;

        // Vanguard I — Handshake Etiquette.
        if (player.HasVanguard
            && weapon.Tags.HasFlag(LancerWeaponTags.Cqb)
            && dist <= VanguardCqbRange)
            acc += 1;

        // Melee ignores cover. Vanguard II — See-Through Seeker for CQB within 3.
        var ignoreCover = isMelee
                          || (player.HasVanguard
                              && weapon.Tags.HasFlag(LancerWeaponTags.Cqb)
                              && dist <= VanguardCqbRange);
        var difficulty = ignoreCover ? 0 : GetCoverDiff(terrain, target.X, target.Y);
        if (!isMelee && IsEngaged(px, py, target.X, target.Y) && !weapon.Tags.HasFlag(LancerWeaponTags.Aux))
            difficulty += 1;
        if (weapon.Tags.HasFlag(LancerWeaponTags.Inaccurate))
            difficulty += 1;

        var (accTotal, diffTotal) = ResolveAccDiff(acc, difficulty);
        var roll = _rng.Next(1, 21);
        var attackTotal = roll + accTotal;
        var evasion = target.Evasion + diffTotal;
        var hit = attackTotal >= evasion;
        var crit = attackTotal >= 20;

        // Nuclear Cavalier checks Danger Zone before this attack's self-heat applies.
        var nuclearBonus = 0;
        var nuclearHeat = 0;
        if (CanTriggerNuclearCavalier(player))
        {
            player.NuclearCavalierUsedThisTurn = true;
            if (hit)
            {
                nuclearHeat = 2;
                // Bonus damage die also crits (roll twice, keep highest).
                nuclearBonus = crit
                    ? Math.Max(_rng.Next(1, 7), _rng.Next(1, 7))
                    : _rng.Next(1, 7);
            }
        }

        var heatSelf = weapon.HeatSelf;
        if (hit && weapon.Tags.HasFlag(LancerWeaponTags.Overkill))
        {
            // Approximate Overkill: ~1/sides chance of a 1 → +1 heat and a free reroll bump.
            if (_rng.Next(weapon.DamageSides) == 0)
                heatSelf += 1;
        }

        if (heatSelf > 0)
            ApplyHeatToPlayer(player, heatSelf);

        if (hit)
        {
            var damage = RollDamage(weapon, crit) + GetTokugawaBonusDamage(player) + nuclearBonus;
            // Mild Overkill bump: if we paid the heat, add +1 expected from the reroll.
            if (weapon.Tags.HasFlag(LancerWeaponTags.Overkill) && heatSelf > weapon.HeatSelf)
                damage += 1;

            ApplyDamageToEnemy(target, damage, ignoreArmor: weapon.Tags.HasFlag(LancerWeaponTags.Ap), enemies);
            if (nuclearHeat > 0 && !target.Destroyed && !target.Fleeing)
                ApplyDamageToEnemy(target, nuclearHeat, ignoreArmor: true, enemies);

            if (weapon.SplashBurst > 0)
                ApplyAnnihilatorSplash(target, enemies, weapon);
        }
        else if (weapon.ReliableMiss > 0)
        {
            ApplyDamageToEnemy(target, weapon.ReliableMiss, ignoreArmor: weapon.Tags.HasFlag(LancerWeaponTags.Ap), enemies);
        }

        if (weapon.Tags.HasFlag(LancerWeaponTags.Loading))
            player.WeaponLoaded[weaponIndex] = false;
    }

    private static void ApplyHeatToPlayer(SimPlayer player, int amount)
    {
        if (amount <= 0)
            return;
        if (player.DeepWellHeatResistThisTurn)
            amount = Math.Max(1, amount / 2);
        player.Heat = Math.Min(player.HeatCap, player.Heat + amount);
    }

    private static bool IsInDangerZone(SimPlayer player) =>
        player.HeatCap > 0 && player.Heat * 2 >= player.HeatCap;

    private static bool CanTriggerNuclearCavalier(SimPlayer player) =>
        player.HasNuclearCavalier && !player.NuclearCavalierUsedThisTurn && IsInDangerZone(player);

    private void ApplyAnnihilatorSplash(SimUnit primary, List<SimUnit> enemies, LancerWeaponDef weapon)
    {
        foreach (var enemy in enemies)
        {
            if (enemy.Destroyed || enemy.Fleeing || ReferenceEquals(enemy, primary))
                continue;
            if (HexDist(primary.X, primary.Y, enemy.X, enemy.Y) > weapon.SplashBurst)
                continue;

            var splash = _rng.Next(1, 4) + 2;
            var roll = _rng.Next(1, 21);
            if (roll < enemy.Evasion)
                continue;

            ApplyDamageToEnemy(enemy, splash, ignoreArmor: weapon.Tags.HasFlag(LancerWeaponTags.Ap), enemies);
        }
    }

    private static int GetEffectiveRange(SimPlayer player, LancerWeaponDef weapon)
    {
        var range = weapon.Range;
        range += GetExternalBatteriesRangeBonus(player, weapon);

        if (player.CoreKind != LancerCoreKind.TokugawaRadiance)
            return range;

        var isMelee = weapon.Tags.HasFlag(LancerWeaponTags.Melee) || weapon.Range <= 1;
        var energyEligible = weapon.Tags.HasFlag(LancerWeaponTags.Energy) || player.Exposed;

        if (player.CoreActive && player.Exposed)
            return range + (isMelee ? 3 : 10);
        if (player.Exposed)
            return range + (isMelee ? 1 : 5);
        if (player.CoreActive && energyEligible)
            return range + (isMelee ? 2 : 5);

        return range;
    }

    private static int GetExternalBatteriesRangeBonus(SimPlayer player, LancerWeaponDef weapon)
    {
        if (!player.HasExternalBatteries || player.ExternalBatteriesDestroyed)
            return 0;
        if (!weapon.Tags.HasFlag(LancerWeaponTags.Energy))
            return 0;

        var isMelee = weapon.Tags.HasFlag(LancerWeaponTags.Melee) || weapon.Range <= 1;
        return isMelee ? 1 : 5;
    }

    private static int GetTokugawaBonusDamage(SimPlayer player) =>
        player.CoreKind == LancerCoreKind.TokugawaRadiance && player.Exposed ? 3 : 0;

    private static void TickExposed(SimPlayer player)
    {
        if (!player.Exposed || player.ExposedTurnsRemaining <= 0)
            return;

        player.ExposedTurnsRemaining--;
        if (player.ExposedTurnsRemaining <= 0)
            player.Exposed = false;
    }

    private static bool HasSimLineOfSight(int fromX, int fromY, int toX, int toY, LancerTerrainType[,] terrain)
    {
        var from = new LancerGridCoord(fromX, fromY);
        var to = new LancerGridCoord(toX, toY);
        foreach (var cell in LancerHex.Line(from, to))
        {
            if (cell.Equals(from) || cell.Equals(to))
                continue;

            // Hex line rounding can briefly leave the board near edges.
            if (!LancerHex.InBounds(cell))
                continue;

            if (terrain[cell.X, cell.Y] == LancerTerrainType.RubbleHard)
                return false;
        }

        return true;
    }

    private void ResolveEnemyAttack(
        SimUnit enemy,
        LancerWeaponDef weapon,
        int px,
        int py,
        SimPlayer player,
        LancerTerrainType[,] terrain)
    {
        if (!weapon.Tags.HasFlag(LancerWeaponTags.Arcing)
            && !HasSimLineOfSight(enemy.X, enemy.Y, px, py, terrain))
            return;

        var difficulty = GetCoverDiff(terrain, px, py);
        if (IsEngaged(enemy.X, enemy.Y, px, py) && !weapon.Tags.HasFlag(LancerWeaponTags.Aux))
            difficulty += 1;

        var (accTotal, diffTotal) = ResolveAccDiff(enemy.AccBonus, difficulty);
        var roll = _rng.Next(1, 21);
        var attackTotal = roll + accTotal;
        var evasion = player.Evasion + diffTotal;
        var hit = attackTotal >= evasion;
        var crit = attackTotal >= 20;

        var damage = 0;
        if (hit)
            damage = RollDamage(weapon, crit);
        else if (weapon.ReliableMiss > 0)
            damage = weapon.ReliableMiss;

        if (damage > 0)
            ApplyDamageToPlayer(player, damage, enemy);

        if (weapon.Tags.HasFlag(LancerWeaponTags.Loading))
            enemy.WeaponLoaded = false;
    }

    private void ResolveEnemyAttackOnRelay(
        SimUnit enemy,
        LancerWeaponDef weapon,
        SimUnit relay,
        LancerTerrainType[,] terrain)
    {
        if (!weapon.Tags.HasFlag(LancerWeaponTags.Arcing)
            && !HasSimLineOfSight(enemy.X, enemy.Y, relay.X, relay.Y, terrain))
            return;

        var difficulty = GetCoverDiff(terrain, relay.X, relay.Y);
        var (accTotal, diffTotal) = ResolveAccDiff(enemy.AccBonus, difficulty);
        var roll = _rng.Next(1, 21);
        var attackTotal = roll + accTotal;
        var evasion = relay.Evasion + diffTotal;
        var hit = attackTotal >= evasion;
        var crit = attackTotal >= 20;

        var damage = 0;
        if (hit)
            damage = RollDamage(weapon, crit);
        else if (weapon.ReliableMiss > 0)
            damage = weapon.ReliableMiss;

        if (damage <= 0)
            return;

        relay.Hp -= Math.Max(0, damage - relay.Armor);
        if (relay.Hp <= 0)
            relay.Destroyed = true;
    }

    private void ApplyHex(int cx, int cy, List<SimUnit> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (enemy.Destroyed || enemy.Fleeing)
                continue;
            if (HexDist(cx, cy, enemy.X, enemy.Y) > 1)
                continue;

            var save = _rng.Next(1, 21);
            var full = _rng.Next(1, 7);
            var dealt = save >= 10 ? full / 2 : full;
            ApplyDamageToEnemy(enemy, dealt, enemies: enemies);
        }
    }

    private static (int X, int Y)? FindHexCluster(List<SimUnit> alive)
    {
        for (var i = 0; i < alive.Count; i++)
        {
            for (var j = i + 1; j < alive.Count; j++)
            {
                if (HexDist(alive[i].X, alive[i].Y, alive[j].X, alive[j].Y) <= 2)
                {
                    // Blast center between them (use first unit's cell).
                    return (alive[i].X, alive[i].Y);
                }
            }
        }

        return null;
    }

    private void ApplyDamageToEnemy(SimUnit unit, int damage, bool ignoreArmor = false, List<SimUnit>? enemies = null)
    {
        if (unit.Kind == LancerUnitKind.Cutlass && unit.Hunkered)
            damage = Math.Max(0, damage - CutlassHunkerReduction);

        var dealt = Math.Max(0, damage - (ignoreArmor || unit.Shredded ? 0 : unit.Armor));
        unit.Hp -= dealt;
        if (unit.Hp > 0)
            return;

        // Cutlass flees instead of dying when all non-Cutlass enemies are gone.
        if (unit.Kind == LancerUnitKind.Cutlass
            && enemies != null
            && enemies.Where(e => IsEnemyKind(e.Kind) && e.Kind != LancerUnitKind.Cutlass)
                .All(e => e.Destroyed || e.Fleeing))
        {
            unit.Fleeing = true;
            unit.Hp = 0;
            return;
        }

        unit.Destroyed = true;
    }

    private void ApplyDamageToPlayer(SimPlayer player, int damage, SimUnit? attacker = null)
    {
        if (damage <= 0)
            return;

        // Core Brace: Resistance (halve) once per round; blocks further reactions.
        if (damage >= 5 && !player.BraceUsedThisRound && player.OverwatchUsesThisRound == 0)
        {
            damage = Math.Max(1, damage / 2);
            player.BraceUsedThisRound = true;
        }

        if (attacker is { Kind: LancerUnitKind.Cutlass } && damage >= 4)
            attacker.Hunkered = true;

        // Core Exposed: double damage, then Armor.
        if (player.Exposed)
            damage *= 2;

        var dealt = Math.Max(0, damage - player.Armor);
        if (dealt <= 0)
            return;

        player.Hp -= dealt;
        while (player.Hp <= 0 && player.Structure > 0)
        {
            player.Structure--;
            if (player.Structure <= 0)
            {
                player.Hp = 0;
                return;
            }

            player.Hp = player.MaxHp;
            TriggerExternalBatteriesExplosion(player);
            _rng.Next(1, 7); // structure check die (ignored in soak)
        }
    }

    private void TriggerExternalBatteriesExplosion(SimPlayer player)
    {
        if (!player.HasExternalBatteries || player.ExternalBatteriesDestroyed)
            return;

        player.ExternalBatteriesDestroyed = true;
        var boom = _rng.Next(1, 7);
        player.Hp -= boom;
        if (player.Hp > 0)
            return;

        if (player.Structure <= 0)
        {
            player.Hp = 0;
            return;
        }

        player.Structure--;
        if (player.Structure <= 0)
        {
            player.Hp = 0;
            return;
        }

        player.Hp = player.MaxHp;
    }

    /// <summary>
    /// Core critical hits: roll 2N damage dice, keep the N highest. Flat damage is not doubled.
    /// </summary>
    private int RollDamage(LancerWeaponDef weapon, bool crit)
    {
        var total = weapon.DamageFlat;
        if (weapon.DamageDice <= 0)
            return total;

        if (!crit)
        {
            for (var i = 0; i < weapon.DamageDice; i++)
                total += _rng.Next(1, weapon.DamageSides + 1);
            return total;
        }

        var pool = new int[weapon.DamageDice * 2];
        for (var i = 0; i < pool.Length; i++)
            pool[i] = _rng.Next(1, weapon.DamageSides + 1);
        Array.Sort(pool);
        for (var i = 0; i < weapon.DamageDice; i++)
            total += pool[pool.Length - weapon.DamageDice + i];
        return total;
    }

    private (int AccTotal, int DiffTotal) ResolveAccDiff(int accuracy, int difficulty)
    {
        var cancelled = Math.Min(Math.Max(0, accuracy), Math.Max(0, difficulty));
        var accCount = Math.Min(6, Math.Max(0, accuracy) - cancelled);
        var diffCount = Math.Min(6, Math.Max(0, difficulty) - cancelled);

        var accTotal = 0;
        for (var i = 0; i < accCount; i++)
            accTotal = Math.Max(accTotal, _rng.Next(1, 7));

        var diffTotal = 0;
        if (diffCount > 0)
        {
            diffTotal = 7;
            for (var i = 0; i < diffCount; i++)
                diffTotal = Math.Min(diffTotal, _rng.Next(1, 7));
            if (diffTotal == 7)
                diffTotal = 0;
        }

        return (accTotal, diffTotal);
    }

    private static int GetCoverDiff(LancerTerrainType[,] terrain, int x, int y)
    {
        if (x < 0 || y < 0 || x >= GridSize || y >= GridSize)
            return 0;

        return terrain[x, y] switch
        {
            LancerTerrainType.RubbleSoft => 1,
            LancerTerrainType.RubbleHard => 2,
            _ => 0
        };
    }

    private static bool IsEngaged(int ax, int ay, int bx, int by) =>
        HexDist(ax, ay, bx, by) <= 1;

    private static void StepToward(
        ref int x,
        ref int y,
        int tx,
        int ty,
        int speed,
        LancerTerrainType[,] terrain,
        List<SimUnit> enemies,
        SimUnit? relay,
        bool avoidHard,
        SimUnit? self = null)
    {
        for (var step = 0; step < speed; step++)
        {
            if (x == tx && y == ty)
                return;

            var bestX = x;
            var bestY = y;
            var bestDist = HexDist(x, y, tx, ty);

            foreach (var (nx, ny) in Neighbors(x, y))
            {
                if (nx < 0 || ny < 0 || nx >= GridSize || ny >= GridSize)
                    continue;
                if (avoidHard && terrain[nx, ny] == LancerTerrainType.RubbleHard)
                    continue;
                if (IsOccupied(nx, ny, enemies, relay, self))
                    continue;

                var dist = HexDist(nx, ny, tx, ty);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestX = nx;
                    bestY = ny;
                }
            }

            if (bestX == x && bestY == y)
                return;

            x = bestX;
            y = bestY;
        }
    }

    private static void StepAway(
        ref int x,
        ref int y,
        int fromX,
        int fromY,
        int speed,
        LancerTerrainType[,] terrain,
        List<SimUnit> enemies,
        SimUnit? relay)
    {
        for (var step = 0; step < speed; step++)
        {
            var bestX = x;
            var bestY = y;
            var bestDist = HexDist(x, y, fromX, fromY);

            foreach (var (nx, ny) in Neighbors(x, y))
            {
                if (nx < 0 || ny < 0 || nx >= GridSize || ny >= GridSize)
                    continue;
                if (terrain[nx, ny] == LancerTerrainType.RubbleHard)
                    continue;
                if (IsOccupied(nx, ny, enemies, relay, null))
                    continue;

                var dist = HexDist(nx, ny, fromX, fromY);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    bestX = nx;
                    bestY = ny;
                }
            }

            if (bestX == x && bestY == y)
                return;

            x = bestX;
            y = bestY;
        }
    }

    private static void SeekSoftCover(
        ref int x,
        ref int y,
        LancerTerrainType[,] terrain,
        List<SimUnit> enemies,
        SimUnit? relay)
    {
        if (GetCoverDiff(terrain, x, y) > 0)
            return;

        foreach (var (nx, ny) in Neighbors(x, y))
        {
            if (nx < 0 || ny < 0 || nx >= GridSize || ny >= GridSize)
                continue;
            if (terrain[nx, ny] != LancerTerrainType.RubbleSoft)
                continue;
            if (IsOccupied(nx, ny, enemies, relay, null))
                continue;

            x = nx;
            y = ny;
            return;
        }
    }

    private static bool IsOccupied(int x, int y, List<SimUnit> enemies, SimUnit? relay, SimUnit? self)
    {
        if (relay is { Destroyed: false } && relay.X == x && relay.Y == y)
            return true;

        foreach (var e in enemies)
        {
            if (e.Destroyed || e.Fleeing || ReferenceEquals(e, self))
                continue;
            if (e.X == x && e.Y == y)
                return true;
        }

        return false;
    }

    private static IEnumerable<(int X, int Y)> Neighbors(int x, int y)
    {
        foreach (var n in LancerHex.Neighbors(new LancerGridCoord(x, y)))
            yield return (n.X, n.Y);
    }

    private static int HexDist(int ax, int ay, int bx, int by) =>
        LancerHex.Distance(new LancerGridCoord(ax, ay), new LancerGridCoord(bx, by));

    private sealed class SimPlayer
    {
        public int Hp;
        public int MaxHp;
        public int Structure;
        public int MaxStructure;
        public int Armor;
        public int Evasion;
        public int Speed;
        public int Heat;
        public int HeatCap;
        public int Repairs;
        public int RepairCap;
        public int HexCharges;
        public int HexCap;
        public int OverwatchRange;
        public bool HasVanguard;
        public bool HasSentinel;
        public LancerCoreKind CoreKind;
        public bool HasNuclearCavalier;
        public bool HasExternalBatteries;
        public bool ExternalBatteriesDestroyed;
        public bool HasDeepWellHeatSink;
        public bool DeepWellHeatResistThisTurn;
        public int CorePower;
        public bool CoreActive;
        public bool Exposed;
        public int ExposedTurnsRemaining;
        public bool NuclearCavalierUsedThisTurn;
        public int OverwatchUsesThisRound;
        public bool BraceUsedThisRound;
        public int PlayerTurnIndex;
        public int RadianceMinTurn = 3;
        public required string[] WeaponIds;
        public required bool[] WeaponLoaded;
    }

    private sealed class SimUnit
    {
        public LancerUnitKind Kind;
        public int X;
        public int Y;
        public int Hp;
        public int MaxHp;
        public int Armor;
        public int Evasion;
        public int AccBonus;
        public string WeaponId = WeaponGruntRifle;
        public bool WeaponLoaded = true;
        public bool Destroyed;
        public bool Fleeing;
        public bool Hunkered;
        public bool Shredded;
    }
}
