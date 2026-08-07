using System;
using Content.Shared._Starlight.Arcade.Lancer;
using System.Linq;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    private void InitGrid()
    {
        if (_encounter != null)
        {
            LoadEncounterTerrain(_encounter);
            return;
        }

        InitEmptyGrid();
        SetTerrain(RelayPosition, LancerTerrainType.Relay);
        SetTerrain(RubbleSoftPosition, LancerTerrainType.RubbleSoft);
        SetTerrain(RubbleHardPosition, LancerTerrainType.RubbleHard);
    }

    private void SetTerrain(LancerGridCoord pos, LancerTerrainType terrain)
    {
        if (!LancerHex.InBounds(pos))
            return;

        _cells[pos.Y][pos.X].Terrain = terrain;
    }

    private void SpawnCombatUnits()
    {
        _units.Clear();
        _nextUnitId = 1;

        var deploy = _encounter != null
            ? new LancerGridCoord(_encounter.PlayerDeployX, _encounter.PlayerDeployY)
            : PlayerDeploy;

        AddUnit(LancerUnitKind.PlayerMech, deploy, _playerMaxHp, _playerArmor, _playerEvasion);

        if (_encounter?.HasRelay != false)
        {
            var relayPos = _encounter != null
                ? new LancerGridCoord(_encounter.RelayX, _encounter.RelayY)
                : RelayPosition;
            AddUnit(LancerUnitKind.Relay, relayPos, RelayMaxHp, 0, RelayEvasion);
        }

        if (_encounter != null)
        {
            foreach (var enemy in _encounter.Enemies)
            {
                var stats = GetEnemyStats(enemy.Kind, enemy.Tier, enemy.Veteran);
                var sprite = !string.IsNullOrEmpty(enemy.SpriteState)
                    ? enemy.SpriteState
                    : GetUnitSprite(enemy.Kind);
                AddUnit(enemy.Kind, new LancerGridCoord(enemy.X, enemy.Y), stats.Hp, stats.Armor, stats.Evasion,
                    tier: enemy.Tier, veteran: enemy.Veteran, weaponId: stats.WeaponId, spriteState: sprite);
            }
        }
        else
        {
            var gruntStats = GetEnemyStats(LancerUnitKind.Grunt, 0);
            AddUnit(LancerUnitKind.Grunt, GruntOneStart, gruntStats.Hp, gruntStats.Armor, gruntStats.Evasion, weaponId: gruntStats.WeaponId);
            AddUnit(LancerUnitKind.Grunt, GruntTwoStart, gruntStats.Hp, gruntStats.Armor, gruntStats.Evasion, weaponId: gruntStats.WeaponId);
            var cutlassStats = GetEnemyStats(LancerUnitKind.Cutlass, 0);
            AddUnit(LancerUnitKind.Cutlass, CutlassStart, cutlassStats.Hp, cutlassStats.Armor, cutlassStats.Evasion, weaponId: cutlassStats.WeaponId);
        }

        if (_narrativeCheckFailed)
            QueueGruntFirstStrike();
    }

    private UnitRecord AddUnit(
        LancerUnitKind kind,
        LancerGridCoord pos,
        int hp,
        int armor,
        int evasion,
        int tier = 0,
        bool veteran = false,
        string? weaponId = null,
        string? spriteState = null)
    {
        var unit = new UnitRecord
        {
            Id = _nextUnitId++,
            Kind = kind,
            // Clamp so malformed encounter YAML can't place units outside the grid (crashes cell indexing).
            Position = new LancerGridCoord(
                Math.Clamp(pos.X, 0, GridSize - 1),
                Math.Clamp(pos.Y, 0, GridSize - 1)),
            Hp = hp,
            MaxHp = hp,
            Armor = armor,
            Evasion = evasion,
            Tier = tier,
            Veteran = veteran,
            WeaponId = weaponId ?? WeaponGruntRifle,
            WeaponLoaded = true,
            SpriteState = spriteState ?? GetUnitSprite(kind)
        };

        var weapon = GetWeaponDefById(unit.WeaponId);
        if (weapon?.Tags.HasFlag(LancerWeaponTags.Loading) == true)
            unit.WeaponLoaded = true;

        _units.Add(unit);
        return unit;
    }

    private void RollNarrativeCheck(Action? onComplete = null)
    {
        if (_encounter == null || _narrativeChoiceIndex < 0)
            return;

        var check = _encounter.NarrativeChecks[_narrativeChoiceIndex];
        _spotRolled = true;
        var roll = _random.Next(1, 21);
        var total = roll + check.Modifier;
        _spotRoll = total;

        SendDiceRoll(new LancerArcadeMessages.LancerDiceRollMessage(
            LancerRollKind.Spot,
            Loc.GetString(check.Label),
            roll,
            Array.Empty<int>(),
            Array.Empty<int>(),
            check.Modifier,
            total,
            check.Dc,
            Array.Empty<int>(),
            total >= check.Dc,
            false,
            true));

        QueueResolution(() =>
        {
            if (total >= check.Dc)
            {
                ApplyNarrativeBonus(check);
                AddLog(Loc.GetString("lancer-arcade-log-narrative-success",
                    ("label", Loc.GetString(check.Label)),
                    ("roll", roll),
                    ("total", total)));
            }
            else
            {
                _narrativeCheckFailed = true;
                _spotFailed = true;
                AddLog(Loc.GetString("lancer-arcade-log-narrative-fail",
                    ("label", Loc.GetString(check.Label)),
                    ("roll", roll),
                    ("total", total)));
            }

            BroadcastState();
            onComplete?.Invoke();
        });
    }

    private int MaxQuickActionsThisTurn()
    {
        // Brace aftermath: only one quick action; no Overcharge bonus.
        if (_braceRestrictedTurn)
            return 1;

        var max = BaseQuickActionsPerTurn;
        if (_overchargeUsed)
            max += 1;
        return max;
    }

    private bool CanSpendQuick() =>
        !_fullUsed && _quickActionsUsed < MaxQuickActionsThisTurn();

    private bool TrySpendQuick(LancerPlayerAction action)
    {
        if (!CanSpendQuick())
            return false;

        _quickActionsUsed++;
        if (_overchargeUsed && _quickActionsUsed > BaseQuickActionsPerTurn)
            _overchargeQuickUsed = true;

        MarkActionUsed(action);
        return true;
    }

    private bool TrySpendFull(LancerPlayerAction action)
    {
        // Brace aftermath: no full actions.
        if (_braceRestrictedTurn)
            return false;

        // A full action replaces both quicks — only legal if no quick has been spent yet.
        if (_fullUsed || _quickActionsUsed > 0)
            return false;

        _fullUsed = true;
        _quickActionsUsed = BaseQuickActionsPerTurn;
        MarkActionUsed(action);
        return true;
    }

    private int[] RollAccDice(int count)
    {
        count = Math.Clamp(count, 0, 6);
        if (count <= 0)
            return Array.Empty<int>();

        var dice = new int[count];
        for (var i = 0; i < count; i++)
            dice[i] = _random.Next(1, 7);
        return dice;
    }

    private static int KeepHighest(int[] dice) =>
        dice.Length == 0 ? 0 : dice.Max();

    private static int KeepLowest(int[] dice) =>
        dice.Length == 0 ? 0 : dice.Min();

    /// <summary>
    /// Core Lancer ACC/DIFF: cancel 1:1, then roll remaining dice and keep highest ACC / lowest DIFF.
    /// </summary>
    private (int AccTotal, int DiffTotal, int[] AccDice, int[] DiffDice) ResolveAccDiff(int accuracy, int difficulty)
    {
        var cancelled = Math.Min(Math.Max(0, accuracy), Math.Max(0, difficulty));
        var accCount = Math.Min(6, Math.Max(0, accuracy) - cancelled);
        var diffCount = Math.Min(6, Math.Max(0, difficulty) - cancelled);
        var accDice = RollAccDice(accCount);
        var diffDice = RollAccDice(diffCount);
        return (KeepHighest(accDice), KeepLowest(diffDice), accDice, diffDice);
    }

    private int RollOverchargeHeat()
    {
        // Escalating heat: 1 → 1d3 → 1d6 → 1d6+1 → …
        return _overchargeHeatStep switch
        {
            0 => 1,
            1 => (_random.Next(1, 7) + 1) / 2, // d3
            2 => _random.Next(1, 7),
            _ => _random.Next(1, 7) + (_overchargeHeatStep - 2)
        };
    }

    private void MarkActionUsed(LancerPlayerAction action) => _usedActions.Add(action);

    private bool IsWeaponAvailableForPick(int weaponIndex)
    {
        if (GetWeaponDef(weaponIndex) == null)
            return false;

        if (IsOrdnanceWeapon(weaponIndex))
            return GetOrdnanceBlockReason(weaponIndex) == LancerWeaponBlockReason.None;

        if (IsLoadingWeapon(weaponIndex) && !_weaponLoaded[weaponIndex])
            return false;

        return true;
    }

    private void HandleWeaponPick(int weaponIndex)
    {
        if (!IsWeaponAvailableForPick(weaponIndex))
            return;

        if (_attackIntent == LancerAttackIntent.Skirmish)
        {
            // Spend the quick on ConfirmTarget so Cancel after picking a mount doesn't eat the action.
            if (!CanSpendQuick())
                return;

            _selectedWeaponIndex = weaponIndex;
            _attackIntent = LancerAttackIntent.None;
            _selectionMode = LancerSelectionMode.Target;
            HighlightAttackTargets(GetWeaponRange(weaponIndex), ignoreLos: IsArcingWeapon(weaponIndex));
            BroadcastState();
            return;
        }

        if (_attackIntent != LancerAttackIntent.Barrage)
            return;

        if (_barragePickedWeapons.Contains(weaponIndex))
            return;

        _barragePickedWeapons.Add(weaponIndex);

        if (_barragePickedWeapons.Count < 2)
        {
            BroadcastState();
            return;
        }

        if (!TrySpendFull(LancerPlayerAction.Barrage))
        {
            _barragePickedWeapons.RemoveAt(_barragePickedWeapons.Count - 1);
            return;
        }

        _attackIntent = LancerAttackIntent.None;
        _selectionMode = LancerSelectionMode.Target;
        HighlightBarrageTargets(_barragePickedWeapons[0], _barragePickedWeapons[1]);
        BroadcastState();
    }

    private void StartBarrageSequence(int targetUnitId, int weapon0, int weapon1)
    {
        _selectionMode = LancerSelectionMode.None;
        _selectedWeaponIndex = -1;
        ClearHighlights();

        _barrageWeaponQueue.Clear();
        _barrageWeaponQueue.Enqueue(weapon0);
        _barrageWeaponQueue.Enqueue(weapon1);
        _barrageCurrentTargetId = targetUnitId;
        _barragePickedWeapons.Clear();

        TryFireNextBarrageWeapon();
    }

    private void StartAuxSkirmishSequence(int targetUnitId)
    {
        _selectionMode = LancerSelectionMode.None;
        _selectedWeaponIndex = -1;
        ClearHighlights();

        _barrageWeaponQueue.Clear();
        _barrageWeaponQueue.Enqueue(2);
        _barrageWeaponQueue.Enqueue(2);
        _barrageCurrentTargetId = targetUnitId;

        TryFireNextBarrageWeapon();
    }

    private void TryFireNextBarrageWeapon()
    {
        if (_barrageWeaponQueue.Count == 0 || _pendingResolution != null || _reactionPromptActive)
            return;

        var weapon = _barrageWeaponQueue.Peek();
        var target = GetUnit(_barrageCurrentTargetId);
        if (target == null || target.Destroyed)
        {
            _selectionMode = LancerSelectionMode.Target;
            HighlightAttackTargets(GetWeaponRange(weapon), ignoreLos: IsArcingWeapon(weapon));
            BroadcastState();
            return;
        }

        _barrageWeaponQueue.Dequeue();
        ResolvePlayerWeaponAttack(weapon, _barrageCurrentTargetId, suppressCleanup: _barrageWeaponQueue.Count > 0, spendSkirmish: false);
    }

    private void ResolvePlayerWeaponAttack(int weaponIndex, int targetUnitId, bool suppressCleanup = false, bool spendSkirmish = true)
    {
        var player = GetPlayerUnit();
        var target = GetUnit(targetUnitId);
        var weapon = GetWeaponDef(weaponIndex);
        if (player == null || target == null || target.Destroyed || target.Fleeing || weapon == null)
            return;

        // Target id comes from the client; only allow attacks on hostile units.
        if (!IsHostile(player, target))
            return;

        if (LancerHex.Distance(player.Position, target.Position) > GetWeaponRange(weaponIndex))
            return;

        if (!IsArcingWeapon(weaponIndex) && !HasLineOfSight(player.Position, target.Position) && !IsAuxWeapon(weaponIndex))
            return;

        // Commit the Skirmish quick action now that a legal target is confirmed.
        if (spendSkirmish && !TrySpendQuick(LancerPlayerAction.Skirmish))
            return;

        var dist = LancerHex.Distance(player.Position, target.Position);
        // Raijin/Everest Power Up: +1 ACC while core active. Grit on all attacks.
        var accBonus = (_coreKind == LancerCoreKind.Raijin && _coreActive ? 1 : 0)
                       + _playerGrit
                       + (_holdAndLock && weaponIndex == 1 ? 1 : 0)
                       + (_firstPlayerAttackBonus ? 1 : 0)
                       + _fightAccBonus
                       ;

        if (weapon.AccWithin > 0 && dist <= weapon.AccWithin)
            accBonus += 1;

        // Vanguard I — Handshake Etiquette: +1 ACC with CQB within 3.
        if (_hasVanguard && IsCqbWeapon(weapon) && IsVanguardCqbBand(dist))
            accBonus += 1;

        // Lock On: consume for +1 ACC on this attack.
        var consumeLockOn = target.LockedOn;
        if (consumeLockOn)
            accBonus += 1;

        if (_firstPlayerAttackBonus)
            _firstPlayerAttackBonus = false;

        BeginAttackResolution(player, target, weapon, weaponIndex, accBonus, isPlayerRoll: true,
            consumeLockOn: consumeLockOn);

        // Loading unload happens in ApplyResolvedAttack so Overwatch shares the path.

        if (!suppressCleanup)
        {
            _selectionMode = LancerSelectionMode.None;
            _selectedWeaponIndex = -1;
            _barragePickedWeapons.Clear();
            ClearHighlights();
        }

        _weaponUsedThisTurn[weaponIndex] = true;
    }

    private void BeginAttackResolution(
        UnitRecord attacker,
        UnitRecord target,
        LancerWeaponDef weapon,
        int weaponIndex,
        int accBonus,
        bool isPlayerRoll,
        Action? onApplied = null,
        bool consumeLockOn = false)
    {
        var dist = LancerHex.Distance(attacker.Position, target.Position);
        var isMelee = weapon.Tags.HasFlag(LancerWeaponTags.Melee) || weapon.Range <= 1;

        // Melee ignores cover. Vanguard II — See-Through Seeker: ignore cover with CQB within 3.
        var ignoreCover = isMelee
                          || (isPlayerRoll
                              && _hasVanguard
                              && IsCqbWeapon(weapon)
                              && IsVanguardCqbBand(dist)
                              && (weapon.Tags.HasFlag(LancerWeaponTags.Arcing)
                                  || HasLineOfSight(attacker.Position, target.Position)));

        var difficulty = (ignoreCover ? 0 : GetCoverDiff(target.Position))
                         + (!isMelee && IsEngaged(attacker, target) && !weapon.Tags.HasFlag(LancerWeaponTags.Aux) ? 1 : 0);

        if (weapon.Tags.HasFlag(LancerWeaponTags.Inaccurate))
            difficulty += 1;

        if (isPlayerRoll && _playerImpaired)
            difficulty += 1;
        if (!isPlayerRoll && attacker.Impaired)
            difficulty += 1;

        // Brace lasting effect: attacks against you at +1 DIFF until end of your next turn.
        if (!isPlayerRoll && target.Kind == LancerUnitKind.PlayerMech && _braceDefenseActive)
            difficulty += 1;

        var (accTotal, diffTotal, accDice, diffDice) = ResolveAccDiff(accBonus, difficulty);

        var roll = _random.Next(1, 21);
        var attackTotal = roll + accTotal;
        var evasion = target.Evasion + diffTotal;
        var hit = attackTotal >= evasion;
        // Core: 20+ on a melee or ranged attack is a critical hit (any attacker).
        var crit = attackTotal >= 20;

        // Tutorial Brace lesson: guarantee a heavy hit so the reaction prompt always appears.
        if (_tutorialForceBraceHit && !isPlayerRoll && target.Kind == LancerUnitKind.PlayerMech)
        {
            hit = true;
            roll = 18;
            attackTotal = Math.Max(attackTotal, evasion);
            crit = false;
        }

        var weaponName = Loc.GetString(weapon.NameLoc);

        int[] rolledDamage = Array.Empty<int>();
        var damage = 0;
        var nuclearBonus = 0;
        var nuclearHeat = 0;
        var nuclearTriggered = false;
        if (hit)
        {
            rolledDamage = RollDamageDice(weapon.DamageDice, weapon.DamageSides, crit);
            if (isPlayerRoll && weapon.Tags.HasFlag(LancerWeaponTags.Overkill))
                rolledDamage = ApplyOverkillRerolls(rolledDamage, weapon.DamageSides);
            damage = rolledDamage.Sum() + weapon.DamageFlat;
            if (isPlayerRoll)
            {
                damage += GetTokugawaBonusDamage(weapon);
                // Core: bonus damage dice also roll twice on a crit; keep the highest.
                if (TryConsumeNuclearCavalier(out nuclearBonus, out nuclearHeat, crit))
                {
                    nuclearTriggered = true;
                    damage += nuclearBonus;
                }
            }

            if (target.Kind == LancerUnitKind.Cutlass && target.Hunkered)
                damage = Math.Max(0, damage - CutlassHunkerReduction);

            if (_tutorialForceBraceHit && !isPlayerRoll && target.Kind == LancerUnitKind.PlayerMech)
                damage = Math.Max(damage, 6);
        }
        else if (weapon.ReliableMiss > 0)
        {
            damage = weapon.ReliableMiss;
        }

        // Miss still consumes Nuclear Cavalier opportunity if the attack was made in the Danger Zone.
        if (!hit && isPlayerRoll && CanTriggerNuclearCavalier())
        {
            _nuclearCavalierUsedThisTurn = true;
            nuclearTriggered = true;
        }

        SendDiceRoll(new LancerArcadeMessages.LancerDiceRollMessage(
            LancerRollKind.Attack,
            weaponName,
            roll,
            accDice,
            diffDice,
            accTotal,
            attackTotal,
            evasion,
            hit ? rolledDamage : Array.Empty<int>(),
            hit || weapon.ReliableMiss > 0,
            crit,
            isPlayerRoll));

        var fromPos = new LancerGridCoord(attacker.Position.X, attacker.Position.Y);
        var toPos = new LancerGridCoord(target.Position.X, target.Position.Y);
        var finalDamage = damage;
        var finalHit = hit;
        var finalCrit = crit;
        var finalAcc = accTotal;
        var finalDiff = diffTotal;
        var finalRoll = roll;
        var blastRadius = weapon.BlastRadius;
        var finalNuclearBonus = nuclearBonus;
        var finalNuclearHeat = nuclearHeat;
        var finalNuclearTriggered = nuclearTriggered;
        var shouldConsumeLock = consumeLockOn;

        QueueResolution(() =>
        {
            if (shouldConsumeLock)
                target.LockedOn = false;

            if (!isPlayerRoll
                && target.Kind == LancerUnitKind.PlayerMech
                && finalDamage >= 5
                && !_reactionPromptActive
                && CanOfferBrace())
            {
                _pendingBraceDamage = finalDamage;
                _pendingBraceRoll = finalRoll;
                _pendingBraceAcc = finalAcc;
                _pendingBraceDiff = finalDiff;
                _pendingBraceHit = finalHit;
                _pendingBraceCrit = finalCrit;
                _pendingBraceAttacker = attacker;
                _pendingBraceTarget = target;
                _reactionUnitId = attacker.Id;
                OfferReaction(LancerReactionType.Brace, PendingReactionContext.BraceDamage, null);
                return;
            }

            ApplyResolvedAttack(
                attacker, target, weaponIndex, weaponName,
                finalRoll, finalAcc, finalDiff, finalHit, finalCrit, finalDamage,
                weapon.Effect, fromPos, toPos, braced: false, blastRadius: blastRadius, blastCenter: toPos,
                splashBurst: weapon.SplashBurst, heatSelf: isPlayerRoll ? weapon.HeatSelf : 0,
                ignoreArmor: weapon.Tags.HasFlag(LancerWeaponTags.Ap),
                nuclearBonus: finalNuclearBonus, nuclearHeat: finalNuclearHeat,
                nuclearTriggered: finalNuclearTriggered);
            onApplied?.Invoke();
        });
    }

    private bool CanTriggerNuclearCavalier() =>
        _hasNuclearCavalier && !_nuclearCavalierUsedThisTurn && IsInDangerZone();

    /// <summary>
    /// Nuclear Cavalier I+II: first attack roll while in the Danger Zone this turn.
    /// On hit: +2 heat to the target and +1d6 energy bonus damage (crits with the attack).
    /// </summary>
    private bool TryConsumeNuclearCavalier(out int bonusDamage, out int bonusHeat, bool crit)
    {
        bonusDamage = 0;
        bonusHeat = 0;
        if (!CanTriggerNuclearCavalier())
            return false;

        _nuclearCavalierUsedThisTurn = true;
        bonusHeat = 2;
        bonusDamage = RollDamageDice(1, 6, crit).Sum();
        return true;
    }

    private string FormatAttackMods(int accTotal, int diffTotal)
    {
        var parts = new List<string>();
        if (accTotal > 0)
            parts.Add(Loc.GetString("lancer-arcade-log-acc-part", ("acc", accTotal)));
        if (diffTotal > 0)
            parts.Add(Loc.GetString("lancer-arcade-log-diff-part", ("diff", diffTotal)));

        return parts.Count > 0 ? " " + string.Join(" ", parts) : string.Empty;
    }

    private void ApplyResolvedAttack(
        UnitRecord attacker,
        UnitRecord target,
        int weaponIndex,
        string weaponName,
        int roll,
        int accTotal,
        int diffTotal,
        bool hit,
        bool crit,
        int damage,
        LancerAttackEffectKind effect,
        LancerGridCoord fromPos,
        LancerGridCoord toPos,
        bool braced,
        int blastRadius = 0,
        LancerGridCoord? blastCenter = null,
        int splashBurst = 0,
        int heatSelf = 0,
        bool ignoreArmor = false,
        int nuclearBonus = 0,
        int nuclearHeat = 0,
        bool nuclearTriggered = false)
    {
        // Core Brace: Resistance to all damage from the triggering attack (halve).
        if (braced)
            damage = Math.Max(1, damage / 2);

        SendAttackEffect(toPos, effect, fromPos);
        var mods = FormatAttackMods(accTotal, diffTotal);

        if (heatSelf > 0 && attacker.Kind == LancerUnitKind.PlayerMech)
            ApplyHeat(heatSelf);

        // Loading: empty after any player attack path (Skirmish, Barrage, Overwatch).
        if (attacker.Kind == LancerUnitKind.PlayerMech
            && weaponIndex >= 0
            && IsLoadingWeapon(weaponIndex))
            _weaponLoaded[weaponIndex] = false;

        if (!hit)
        {
            if (damage > 0)
            {
                ApplyDamage(target, damage, attacker, ignoreArmor);
                AddLog(Loc.GetString("lancer-arcade-log-attack-miss-reliable",
                    ("weapon", weaponName),
                    ("roll", roll),
                    ("mods", mods),
                    ("eva", target.Evasion),
                    ("damage", damage)));
            }
            else
            {
                AddLog(Loc.GetString("lancer-arcade-log-attack-miss",
                    ("weapon", weaponName),
                    ("roll", roll),
                    ("mods", mods),
                    ("eva", target.Evasion)));
            }

            if (nuclearTriggered)
                AddLog(Loc.GetString("lancer-arcade-log-nuclear-cavalier-miss"));
        }
        else
        {
            if (target.Kind == LancerUnitKind.Cutlass && target.Hunkered && damage >= 0)
                AddLog(Loc.GetString("lancer-arcade-log-hunker-reduce"));

            ApplyDamage(target, damage, attacker, ignoreArmor);
            AddLog(Loc.GetString(crit ? "lancer-arcade-log-attack-hit-crit" : "lancer-arcade-log-attack-hit",
                ("weapon", weaponName),
                ("roll", roll),
                ("mods", mods),
                ("eva", target.Evasion),
                ("damage", damage)));

            if (nuclearTriggered && (nuclearBonus > 0 || nuclearHeat > 0))
            {
                // Arcade NPCs have no heat gauge — Aggressive Heat Bleed lands as bonus damage.
                if (nuclearHeat > 0 && !target.Destroyed && IsEnemyKind(target.Kind))
                    ApplyDamage(target, nuclearHeat, attacker, ignoreArmor: true);

                AddLog(Loc.GetString("lancer-arcade-log-nuclear-cavalier",
                    ("bonus", nuclearBonus),
                    ("heat", nuclearHeat)));
            }

            // Hyper-Reflex Mode: Overwatch hits Immobilize until end of their next turn.
            if (hit
                && attacker.Kind == LancerUnitKind.PlayerMech
                && target.Id == _hyperReflexImmobilizeUnitId
                && IsEnemyKind(target.Kind)
                && !target.Destroyed
                && !target.Fleeing)
            {
                target.Immobilized = true;
                AddLog(Loc.GetString("lancer-arcade-log-hyper-reflex-immobilize",
                    ("unit", target.Kind.ToString())));
            }

            // Catalytic Hammer: on crit, Hull save or Stunned until end of their next turn.
            if (crit
                && weaponIndex >= 0
                && GetWeaponDef(weaponIndex)?.Tags.HasFlag(LancerWeaponTags.StunOnCrit) == true
                && IsEnemyKind(target.Kind)
                && !target.Destroyed
                && !target.Fleeing)
            {
                TryCatalyticHammerStun(target);
            }

            if (attacker.Kind == LancerUnitKind.Cutlass
                && target.Kind == LancerUnitKind.PlayerMech
                && damage >= 4)
            {
                attacker.Hunkered = true;
            }

            if (damage > 0
                && target.Kind == LancerUnitKind.Cutlass
                && target.Hp <= 0
                && EnemiesExceptCutlassDead())
            {
                target.Fleeing = true;
            }

            if (blastRadius > 0 && blastCenter != null)
                ApplyBlastDamage(attacker, blastCenter, blastRadius, damage / 2);

            // Annihilator: secondary attacks vs characters within Burst 1 of the primary target.
            if (splashBurst > 0 && blastCenter != null)
                ApplyAnnihilatorSplash(attacker, target, blastCenter, splashBurst, weaponName, ignoreArmor);
        }

        PlayArcadeSound("attack");
        CheckEndConditions();

        if (_tutorialActive
            && attacker.Kind == LancerUnitKind.PlayerMech
            && _barrageWeaponQueue.Count == 0)
        {
            if (_tutorialBarragePending)
            {
                _tutorialBarragePending = false;
                NotifyTutorialEvent(TutorialAdvanceEvent.BarrageResolved);
            }
            else
            {
                NotifyTutorialEvent(TutorialAdvanceEvent.AttackResolved);
            }
        }

        BroadcastState();
    }

    private void ApplyAnnihilatorSplash(
        UnitRecord attacker,
        UnitRecord primary,
        LancerGridCoord center,
        int burst,
        string weaponName,
        bool ignoreArmor)
    {
        foreach (var unit in _units.Where(u => !u.Destroyed && u.Id != attacker.Id && u.Id != primary.Id))
        {
            if (LancerHex.Distance(unit.Position, center) > burst)
                continue;

            if (!IsHostile(attacker, unit) && unit.Kind != LancerUnitKind.Relay)
                continue;

            // Secondary attack: 1d3+2, no bonus damage, no heat.
            var splash = _random.Next(1, 4) + 2;
            var roll = _random.Next(1, 21);
            if (roll < unit.Evasion)
                continue;

            ApplyDamage(unit, splash, attacker, ignoreArmor);
            AddLog(Loc.GetString("lancer-arcade-log-annihilator-splash",
                ("weapon", weaponName),
                ("unit", unit.Kind.ToString()),
                ("damage", splash)));
        }
    }

    private void ApplyBlastDamage(UnitRecord attacker, LancerGridCoord center, int radius, int splashDamage)
    {
        if (splashDamage <= 0)
            return;

        foreach (var unit in _units.Where(u => !u.Destroyed && u.Id != attacker.Id))
        {
            if (LancerHex.Distance(unit.Position, center) > radius)
                continue;

            if (unit.Kind == LancerUnitKind.Relay || unit.Kind == LancerUnitKind.PlayerMech || IsHostile(attacker, unit))
            {
                ApplyDamage(unit, splashDamage, attacker);
                AddLog(Loc.GetString("lancer-arcade-log-blast-splash",
                    ("unit", unit.Kind.ToString()),
                    ("damage", splashDamage)));
            }
        }
    }

    /// <summary>
    /// Core critical hits: roll all damage dice twice and keep the highest result from each
    /// die count — i.e. roll 2N dice, keep the N highest (including bonus damage dice).
    /// Flat damage is not doubled.
    /// </summary>
    private int[] RollDamageDice(int dice, int sides, bool crit)
    {
        if (dice <= 0)
            return Array.Empty<int>();

        if (!crit)
        {
            var normal = new int[dice];
            for (var i = 0; i < dice; i++)
                normal[i] = _random.Next(1, sides + 1);
            return normal;
        }

        var pool = new int[dice * 2];
        for (var i = 0; i < pool.Length; i++)
            pool[i] = _random.Next(1, sides + 1);

        Array.Sort(pool);
        var kept = new int[dice];
        for (var i = 0; i < dice; i++)
            kept[i] = pool[pool.Length - dice + i];
        return kept;
    }

    /// <summary>Overkill: any 1 on a damage die is rerolled; each 1 also applies +1 heat to self.</summary>
    private int[] ApplyOverkillRerolls(int[] dice, int sides)
    {
        for (var i = 0; i < dice.Length; i++)
        {
            while (dice[i] == 1)
            {
                ApplyHeat(1);
                dice[i] = _random.Next(1, sides + 1);
            }
        }

        return dice;
    }

    private void ApplyDamage(UnitRecord target, int damage, UnitRecord? attacker, bool ignoreArmor = false)
    {
        if (target.Kind == LancerUnitKind.PlayerMech)
        {
            // MNGR wire hack: player mech takes no combat damage.
            if (GetArcade()?.PlayerInvincible == true)
                return;

            // Core Exposed: double Kinetic/Explosive/Energy damage, then Armor.
            if (_playerExposed)
                damage *= 2;

            var armor = ignoreArmor || target.Shredded ? 0 : _playerArmor;
            var remaining = Math.Max(0, damage - armor);
            _playerHp -= remaining;

            // Structure break: overflow past 0 HP carries into the restored HP pool.
            while (_playerHp <= 0 && _structure > 0)
            {
                var overflow = -_playerHp;
                _structure--;
                _playerHp = _playerMaxHp - overflow;
                TriggerExternalBatteriesExplosion();
                ResolveStructureCheck();
            }

            if (_playerHp < 0)
                _playerHp = 0;

            target.Hp = _playerHp;
            return;
        }

        var effectiveArmor = ignoreArmor || target.Shredded ? 0 : target.Armor;
        var reduced = Math.Max(0, damage - effectiveArmor);
        target.Hp -= reduced;

        if (target.Hp <= 0)
        {
            target.Destroyed = true;
            if (target.Kind == LancerUnitKind.Cutlass && target.Fleeing)
                AddLog(Loc.GetString("lancer-arcade-log-cutlass-flees"));
            else if (target.Kind != LancerUnitKind.Relay)
                AddLog(Loc.GetString("lancer-arcade-log-unit-destroyed", ("unit", target.Kind.ToString())));
        }
    }

    /// <summary>
    /// External Batteries: on structure damage, system is destroyed and deals 1d6 explosive AP to self.
    /// </summary>
    private void TriggerExternalBatteriesExplosion()
    {
        if (!_hasExternalBatteries || _externalBatteriesDestroyed)
            return;

        _externalBatteriesDestroyed = true;
        var boom = _random.Next(1, 7);
        _playerHp -= boom;
        AddLog(Loc.GetString("lancer-arcade-log-external-batteries", ("damage", boom)));

        // Cascade: overflow past 0 HP carries into the next structure pool (batteries already spent).
        while (_playerHp <= 0 && _structure > 0)
        {
            var overflow = -_playerHp;
            _structure--;
            _playerHp = _playerMaxHp - overflow;
            ResolveStructureCheck();
        }

        if (_playerHp < 0)
            _playerHp = 0;

        var player = GetPlayerUnit();
        if (player != null)
            player.Hp = _playerHp;
    }

    private void ResolveStructureCheck()
    {
        // Core: roll 1d6 per structure damage marked (simplified: 1d6 for single break).
        var roll = _random.Next(1, 7);
        SendDiceRoll(new LancerArcadeMessages.LancerDiceRollMessage(
            LancerRollKind.StructureCheck,
            Loc.GetString("lancer-arcade-roll-structure"),
            0,
            [roll],
            Array.Empty<int>(),
            0,
            roll,
            0,
            Array.Empty<int>(),
            false,
            false,
            true));

        switch (roll)
        {
            case 5:
            case 6:
                // Glancing Blow — Impaired until end of next turn.
                _playerImpaired = true;
                AddLog(Loc.GetString("lancer-arcade-log-structure-glancing", ("roll", roll)));
                break;
            case 2:
            case 3:
            case 4:
                // System Trauma — destroy a loaded weapon mount (or Impaired if none).
                if (!TryDestroyWeaponMount())
                    _playerImpaired = true;
                AddLog(Loc.GetString("lancer-arcade-log-structure-trauma", ("roll", roll)));
                break;
            case 1:
                ResolveDirectHit();
                break;
        }
    }

    private bool TryDestroyWeaponMount()
    {
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            if (GetWeaponDef(i) == null)
                continue;
            if (!_weaponLoaded[i] && IsLoadingWeapon(i))
                continue;

            _weaponLoaded[i] = false;
            // Empty the mount permanently for this fight by clearing the id.
            var name = Loc.GetString(GetWeaponDef(i)!.NameLoc);
            _weaponIds[i] = string.Empty;
            AddLog(Loc.GetString("lancer-arcade-log-structure-weapon-destroyed", ("weapon", name)));
            return true;
        }

        return false;
    }

    private void ResolveDirectHit()
    {
        // Direct Hit depends on remaining Structure after this break was applied.
        if (_structure >= 3)
        {
            _playerImpaired = true;
            AddLog(Loc.GetString("lancer-arcade-log-structure-direct-stun", ("structure", _structure)));
        }
        else if (_structure == 2)
        {
            var save = _random.Next(1, 21) + _playerGrit;
            if (save >= 10)
            {
                _playerImpaired = true;
                AddLog(Loc.GetString("lancer-arcade-log-structure-direct-hull-pass", ("total", save)));
            }
            else
            {
                _structure = 0;
                _playerHp = 0;
                AddLog(Loc.GetString("lancer-arcade-log-structure-direct-hull-fail", ("total", save)));
            }
        }
        else
        {
            _structure = 0;
            _playerHp = 0;
            AddLog(Loc.GetString("lancer-arcade-log-structure-direct-destroy"));
        }
    }

    private void ApplyStabilize(LancerStabilizeOption option)
    {
        switch (option)
        {
            case LancerStabilizeOption.ClearHeat:
                _heat = 0;
                _playerExposed = false;
                _exposedTurnsRemaining = 0;
                _playerImpaired = false;
                AddLog(Loc.GetString("lancer-arcade-log-stabilize-heat"));
                _selectionMode = LancerSelectionMode.None;
                BroadcastState();
                NotifyTutorialEvent(TutorialAdvanceEvent.StabilizedClearHeat);
                return;
            case LancerStabilizeOption.Repair when _repairs > 0:
                _repairs--;
                _playerHp = _playerMaxHp;
                _playerImpaired = false;
                SyncPlayerUnitHp();
                AddLog(Loc.GetString("lancer-arcade-log-stabilize-repair"));
                break;
            case LancerStabilizeOption.Reload:
            {
                var reloaded = 0;
                for (var i = 0; i < WeaponSlotCount; i++)
                {
                    if (!IsLoadingWeapon(i))
                        continue;
                    if (!_weaponLoaded[i])
                        reloaded++;
                    _weaponLoaded[i] = true;
                }

                AddLog(Loc.GetString(reloaded > 0
                    ? "lancer-arcade-log-stabilize-reload"
                    : "lancer-arcade-log-stabilize-reload-none"));
                break;
            }
        }

        _selectionMode = LancerSelectionMode.None;
        BroadcastState();
    }

    private void ResolveHex(LancerGridCoord cell)
    {
        if (_hexCharges <= 0 || !TrySpendQuick(LancerPlayerAction.UseSystem))
            return;

        _hexCharges--;

        var targets = _units
            .Where(u => !u.Destroyed && LancerHex.Distance(u.Position, cell) <= 1 && u.Kind != LancerUnitKind.Relay)
            .ToList();

        SendAttackEffect(cell, LancerAttackEffectKind.HexBlast);

        if (targets.Count == 0)
        {
            _selectionMode = LancerSelectionMode.None;
            ClearHighlights();
            AddLog(Loc.GetString("lancer-arcade-log-hex-empty"));
            BroadcastState();
            NotifyTutorialEvent(TutorialAdvanceEvent.HexResolved);
            return;
        }

        ResolveHexSaveChain(cell, targets, 0);
    }

    private void ResolveHexSaveChain(LancerGridCoord cell, List<UnitRecord> targets, int index)
    {
        if (index >= targets.Count)
        {
            _selectionMode = LancerSelectionMode.None;
            ClearHighlights();
            CheckEndConditions();
            BroadcastState();
            NotifyTutorialEvent(TutorialAdvanceEvent.HexResolved);
            return;
        }

        var unit = targets[index];
        var saveDc = 10;
        var save = _random.Next(1, 21);
        var passed = save >= saveDc;
        var damageDice = RollDamageDice(1, 6, false);
        var full = damageDice.Sum();
        var dealt = passed ? full / 2 : full;

        SendDiceRoll(new LancerArcadeMessages.LancerDiceRollMessage(
            LancerRollKind.Save,
            Loc.GetString("lancer-arcade-roll-hex-save", ("unit", unit.Kind.ToString())),
            save,
            Array.Empty<int>(),
            Array.Empty<int>(),
            0,
            save,
            saveDc,
            damageDice,
            !passed,
            false,
            false));

        QueueResolution(() =>
        {
            ApplyDamage(unit, dealt, GetPlayerUnit());
            AddLog(Loc.GetString(passed ? "lancer-arcade-log-hex-save" : "lancer-arcade-log-hex-fail",
                ("unit", unit.Kind.ToString()),
                ("damage", dealt)));
            BroadcastState();
            ResolveHexSaveChain(cell, targets, index + 1);
        });
    }

    private void ResolveDivinePunishment()
    {
        var enemies = _units
            .Where(u => !u.Destroyed && IsEnemyKind(u.Kind))
            .ToList();

        if (enemies.Count == 0)
        {
            AddLog(Loc.GetString("lancer-arcade-log-divine-empty"));
            BroadcastState();
            return;
        }

        ResolveDivinePunishmentChain(enemies, 0);
    }

    private void ResolveDivinePunishmentChain(List<UnitRecord> targets, int index)
    {
        if (index >= targets.Count)
        {
            CheckEndConditions();
            BroadcastState();
            return;
        }

        var unit = targets[index];
        var saveDc = 10;
        var save = _random.Next(1, 21);
        var passed = save >= saveDc;
        // Divine Punishment: 1d6+4 explosive; half on Agility save.
        var damageDice = RollDamageDice(1, 6, false);
        var full = damageDice.Sum() + 4;
        var dealt = passed ? Math.Max(1, full / 2) : full;

        SendDiceRoll(new LancerArcadeMessages.LancerDiceRollMessage(
            LancerRollKind.Save,
            Loc.GetString("lancer-arcade-roll-divine-save", ("unit", unit.Kind.ToString())),
            save,
            Array.Empty<int>(),
            Array.Empty<int>(),
            0,
            save,
            saveDc,
            damageDice,
            !passed,
            false,
            true));

        QueueResolution(() =>
        {
            ApplyDamage(unit, dealt, GetPlayerUnit());
            AddLog(Loc.GetString(passed ? "lancer-arcade-log-divine-save" : "lancer-arcade-log-divine-fail",
                ("unit", unit.Kind.ToString()),
                ("damage", dealt)));
            BroadcastState();
            ResolveDivinePunishmentChain(targets, index + 1);
        });
    }

    private void SyncPlayerUnitHp()
    {
        var player = GetPlayerUnit();
        if (player != null)
            player.Hp = _playerHp;
    }

    private void ApplyHeat(int amount)
    {
        if (amount <= 0)
            return;

        if (_deepWellHeatResistThisTurn)
            amount = Math.Max(1, amount / 2);

        _heat += amount;
        if (_heat < _playerHeatCap)
            return;

        _heat = 0;
        ResolveOverheatCheck();
    }

    private void ResolveOverheatCheck()
    {
        if (_stress > 0)
            _stress--;

        var roll = _random.Next(1, 7);
        SendDiceRoll(new LancerArcadeMessages.LancerDiceRollMessage(
            LancerRollKind.OverheatCheck,
            Loc.GetString("lancer-arcade-roll-overheat"),
            0,
            [roll],
            Array.Empty<int>(),
            0,
            roll,
            0,
            Array.Empty<int>(),
            false,
            false,
            true));

        switch (roll)
        {
            case 5:
            case 6:
                // Emergency Shunt — Impaired until end of next turn.
                _playerImpaired = true;
                AddLog(Loc.GetString("lancer-arcade-log-overheat-vent", ("roll", roll)));
                break;
            case 2:
            case 3:
            case 4:
                // Destabilized Power Plant — Exposed until Stabilize.
                _playerExposed = true;
                _exposedTurnsRemaining = PermanentExposedTurns;
                AddLog(Loc.GetString("lancer-arcade-log-overheat-destabilized", ("roll", roll)));
                break;
            case 1:
                ResolveMeltdownResult();
                break;
        }

        if (_stress <= 0)
            _reactorMeltdownPending = true;
    }

    private void ResolveMeltdownResult()
    {
        if (_stress >= 3)
        {
            _playerExposed = true;
            _exposedTurnsRemaining = PermanentExposedTurns;
            AddLog(Loc.GetString("lancer-arcade-log-overheat-meltdown-exposed"));
        }
        else if (_stress == 2)
        {
            var save = _random.Next(1, 21) + _playerGrit;
            if (save >= 10)
            {
                _playerExposed = true;
                _exposedTurnsRemaining = PermanentExposedTurns;
                AddLog(Loc.GetString("lancer-arcade-log-overheat-meltdown-eng-pass", ("total", save)));
            }
            else
            {
                _reactorMeltdownPending = true;
                AddLog(Loc.GetString("lancer-arcade-log-overheat-meltdown-eng-fail", ("total", save)));
            }
        }
        else
        {
            _reactorMeltdownPending = true;
            AddLog(Loc.GetString("lancer-arcade-log-overheat-meltdown-imminent"));
        }
    }

    /// <summary>Reactor meltdown: pilot dies / mech destroyed (arcade: fight loss + BURST 2 splash).</summary>
    private void TriggerReactorMeltdown()
    {
        if (!_reactorMeltdownPending)
            return;

        _reactorMeltdownPending = false;
        AddLog(Loc.GetString("lancer-arcade-log-overheat-meltdown-boom"));

        var player = GetPlayerUnit();
        if (player != null)
        {
            foreach (var unit in _units.Where(u => !u.Destroyed && IsEnemyKind(u.Kind)))
            {
                if (LancerHex.Distance(player.Position, unit.Position) > 2)
                    continue;
                var dmg = RollDamageDice(4, 6, false).Sum();
                ApplyDamage(unit, dmg, player, ignoreArmor: false);
            }
        }

        _playerHp = 0;
        _structure = 0;
        OnFightLost();
    }

    private void ResolveLockOn(int targetUnitId)
    {
        var player = GetPlayerUnit();
        var target = GetUnit(targetUnitId);
        if (player == null || target == null || target.Destroyed || target.Fleeing)
            return;

        if (!IsHostile(player, target))
            return;

        if (LancerHex.Distance(player.Position, target.Position) > LockOnRange)
            return;

        if (!TrySpendQuick(LancerPlayerAction.LockOn))
            return;

        _selectionMode = LancerSelectionMode.None;
        ClearHighlights();

        // Lock On applies automatically (arcade); consume later for +1 ACC.
        target.LockedOn = true;
        AddLog(Loc.GetString("lancer-arcade-log-lock-on", ("unit", target.Kind.ToString())));
        BroadcastState();
        NotifyTutorialEvent(TutorialAdvanceEvent.LockOnApplied);
    }

    private void HighlightLockOnTargets()
    {
        ClearHighlights();
        var player = GetPlayerUnit();
        if (player == null)
            return;

        foreach (var unit in _units.Where(u => !u.Destroyed && IsHostile(player, u)))
        {
            if (LancerHex.Distance(player.Position, unit.Position) <= LockOnRange)
                _cells[unit.Position.Y][unit.Position.X].Highlight = LancerCellHighlight.Target;
        }

        FilterTutorialHighlights();
    }

    /// <summary>
    /// Hyper-Reflex Mode (Tortuga core): +1 Overwatch/round. Default is 1.
    /// </summary>
    private int MaxOverwatchUsesThisRound() =>
        _coreKind == LancerCoreKind.TortugaSentinel && _coreActive ? 2 : 1;

    private bool CanOfferOverwatch() =>
        !_braceReactionLockout
        && !_reactionUsedThisActivation
        && _overwatchUsesThisRound < MaxOverwatchUsesThisRound();

    private bool CanOfferBrace() =>
        !_braceReactionLockout
        && !_braceUsedThisRound
        && !_reactionUsedThisActivation;

    /// <summary>
    /// Catalytic Hammer crit: Hull save (d20 + tier) vs DC 12, or Stunned for one activation.
    /// </summary>
    private void TryCatalyticHammerStun(UnitRecord target)
    {
        var roll = _random.Next(1, 21);
        var bonus = target.Tier + (target.Veteran ? 2 : 0);
        var total = roll + bonus;
        const int dc = 12;
        if (total >= dc)
        {
            AddLog(Loc.GetString("lancer-arcade-log-hammer-stun-save",
                ("unit", target.Kind.ToString()),
                ("roll", roll),
                ("total", total),
                ("dc", dc)));
            return;
        }

        target.Stunned = true;
        AddLog(Loc.GetString("lancer-arcade-log-hammer-stun",
            ("unit", target.Kind.ToString()),
            ("roll", roll),
            ("total", total),
            ("dc", dc)));
    }

    private int GetCoverDiff(LancerGridCoord pos)
    {
        if (!LancerHex.InBounds(pos))
            return 0;

        return _cells[pos.Y][pos.X].Terrain switch
        {
            LancerTerrainType.RubbleSoft => 1,
            LancerTerrainType.RubbleHard => 2,
            _ => 0
        };
    }

    private bool IsEngaged(UnitRecord a, UnitRecord b) =>
        LancerHex.Distance(a.Position, b.Position) <= 1 && IsHostile(a, b);

    private bool IsEngagedWithHostile(UnitRecord unit) =>
        !_disengaged && _units.Any(u => !u.Destroyed && IsHostile(unit, u) && LancerHex.Distance(unit.Position, u.Position) <= 1);

    private static bool IsHostile(UnitRecord a, UnitRecord b)
    {
        if (a.Kind == LancerUnitKind.PlayerMech)
            return IsEnemyKind(b.Kind);
        if (b.Kind == LancerUnitKind.PlayerMech)
            return IsEnemyKind(a.Kind);
        if (a.Kind == LancerUnitKind.Relay)
            return IsEnemyKind(b.Kind);
        return false;
    }

    private bool AllEnemiesGone() =>
        _units.Where(u => IsEnemyKind(u.Kind))
            .All(u => u.Destroyed || u.Fleeing);

    private bool EnemiesExceptCutlassDead() =>
        _units.Where(u => IsEnemyKind(u.Kind) && u.Kind != LancerUnitKind.Cutlass)
            .All(u => u.Destroyed);

    private static string FormatCoord(LancerGridCoord pos) =>
        $"{(char) ('A' + pos.X)}{pos.Y + 1}";

    private UnitRecord? GetPlayerUnit() => _units.FirstOrDefault(u => u.Kind == LancerUnitKind.PlayerMech && !u.Destroyed);
    private UnitRecord? GetRelay() => _units.FirstOrDefault(u => u.Kind == LancerUnitKind.Relay);
    private UnitRecord? GetUnit(int id) => _units.FirstOrDefault(u => u.Id == id);

    private void ClearHighlights()
    {
        for (var y = 0; y < GridSize; y++)
        for (var x = 0; x < GridSize; x++)
            _cells[y][x].Highlight = LancerCellHighlight.None;
    }

    private void HighlightReachable(LancerGridCoord start, int maxDistance, bool ignoreTerrain)
    {
        ClearHighlights();
        var visited = FloodFill(start, maxDistance, ignoreTerrain);
        foreach (var pos in visited)
        {
            if (!pos.Equals(start))
                _cells[pos.Y][pos.X].Highlight = LancerCellHighlight.Reachable;
        }

        FilterTutorialHighlights();
    }

    private HashSet<LancerGridCoord> FloodFill(LancerGridCoord start, int maxDistance, bool ignoreTerrain)
    {
        var result = new HashSet<LancerGridCoord>();
        var queue = new Queue<(LancerGridCoord Pos, int Dist)>();
        queue.Enqueue((start, 0));
        result.Add(new LancerGridCoord(start.X, start.Y));

        while (queue.Count > 0)
        {
            var (pos, dist) = queue.Dequeue();
            if (dist >= maxDistance)
                continue;

            foreach (var next in LancerHex.Neighbors(pos))
            {
                if (!LancerHex.InBounds(next) || result.Contains(next))
                    continue;

                if (IsOccupied(next) && !next.Equals(start))
                    continue;

                if (!ignoreTerrain && _cells[next.Y][next.X].Terrain == LancerTerrainType.RubbleHard)
                    continue;

                result.Add(next);
                queue.Enqueue((next, dist + 1));
            }
        }

        return result;
    }

    private bool IsReachable(LancerGridCoord start, LancerGridCoord target, int maxDistance, bool ignoreTerrain) =>
        FloodFill(start, maxDistance, ignoreTerrain).Any(p => p.Equals(target));

    private bool IsOccupied(LancerGridCoord pos) =>
        _units.Any(u => !u.Destroyed && u.Kind != LancerUnitKind.Relay && u.Position.Equals(pos));

    private void HighlightAttackTargets(int range, bool ignoreLos = false)
    {
        ClearHighlights();
        var player = GetPlayerUnit();
        if (player == null)
            return;

        foreach (var unit in _units.Where(u => !u.Destroyed && IsHostile(player, u)))
        {
            if (LancerHex.Distance(player.Position, unit.Position) <= range
                && (ignoreLos || range <= 1 || HasLineOfSight(player.Position, unit.Position)))
            {
                _cells[unit.Position.Y][unit.Position.X].Highlight = LancerCellHighlight.Target;
            }
        }

        FilterTutorialHighlights();
    }

    private void HighlightBarrageTargets(int weapon0, int weapon1)
    {
        ClearHighlights();
        var player = GetPlayerUnit();
        if (player == null)
            return;

        var range0 = GetWeaponRange(weapon0);
        var range1 = GetWeaponRange(weapon1);

        foreach (var unit in _units.Where(u => !u.Destroyed && IsHostile(player, u)))
        {
            var dist = LancerHex.Distance(player.Position, unit.Position);
            if (dist > range0 || dist > range1)
                continue;

            if ((range0 > 1
                 && !IsArcingWeapon(weapon0)
                 && !HasLineOfSight(player.Position, unit.Position))
                || (range1 > 1
                    && !IsArcingWeapon(weapon1)
                    && !HasLineOfSight(player.Position, unit.Position)))
                continue;

            _cells[unit.Position.Y][unit.Position.X].Highlight = LancerCellHighlight.Target;
        }

        FilterTutorialHighlights();
    }

    private void HighlightHexTargets()
    {
        ClearHighlights();
        var player = GetPlayerUnit();
        if (player == null)
            return;

        for (var y = 0; y < GridSize; y++)
        for (var x = 0; x < GridSize; x++)
        {
            var pos = new LancerGridCoord(x, y);
            if (LancerHex.Distance(player.Position, pos) <= 5)
                _cells[y][x].Highlight = LancerCellHighlight.Blast;
        }

        FilterTutorialHighlights();
    }

    private bool IsValidHexTarget(LancerGridCoord cell) =>
        _cells[cell.Y][cell.X].Highlight == LancerCellHighlight.Blast;

    private bool HasLineOfSight(LancerGridCoord from, LancerGridCoord to)
    {
        foreach (var cell in LancerHex.Line(from, to))
        {
            if (cell.Equals(from) || cell.Equals(to))
                continue;

            // Hex line rounding can briefly leave the board near edges.
            if (!LancerHex.InBounds(cell))
                continue;

            if (_cells[cell.Y][cell.X].Terrain == LancerTerrainType.RubbleHard)
                return false;
        }

        return true;
    }

    private void QueueResolution(Action apply)
    {
        _pendingResolution = new PendingResolution
        {
            Timer = RollDisplayDelay,
            Apply = apply
        };
    }

    private bool IsBusy => _pendingResolution != null || _processingAi || _reactionPromptActive;
}
