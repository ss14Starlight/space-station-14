using Content.Shared._Starlight.Arcade.Lancer;
using System.Linq;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    private void QueueGruntFirstStrike()
    {
        var grunt = _units.FirstOrDefault(u => IsMookKind(u.Kind) && !u.Destroyed);
        if (grunt == null)
            return;

        _aiQueue.Add(new AiStep { UnitId = grunt.Id, Kind = AiStepKind.Attack, TargetUnitId = GetPlayerUnit()?.Id ?? -1 });
        _processingAi = true;
        _aiStepTimer = 0;
    }

    private void QueueEnemyTurns()
    {
        if (_tutorialActive && ShouldSkipNormalEnemyAi())
        {
            QueueTutorialScriptedAi();
            return;
        }

        _aiQueue.Clear();
        _reservedAiMoveCells.Clear();
        _executingAiUnitId = -1;
        foreach (var unit in _units.Where(u =>
                     !u.Destroyed
                     && !u.Fleeing
                     && IsEnemyKind(u.Kind)))
        {
            // Catalytic Hammer stun: skip this activation, then clear.
            if (unit.Stunned)
            {
                unit.Stunned = false;
                unit.Immobilized = false;
                AddLog(Loc.GetString("lancer-arcade-log-hammer-stun-skip",
                    ("unit", unit.Kind.ToString())));
                continue;
            }

            PlanEnemyActions(unit);
        }

        if (_aiQueue.Count > 0)
        {
            _processingAi = true;
            _aiStepTimer = AiStepDelay;
        }
        else
        {
            _processingAi = false;
            BeginPlayerTurn();
        }
    }

    private void PlanEnemyActions(UnitRecord unit)
    {
        var player = GetPlayerUnit();
        var relay = GetRelay();
        var weapon = GetWeaponDefById(unit.WeaponId);
        if (weapon == null)
            return;

        if (IsArtilleryKind(unit.Kind))
        {
            if (!unit.WeaponLoaded)
            {
                unit.WeaponLoaded = true;
                return;
            }

            var artilleryTarget = player != null && !player.Destroyed ? player : relay;
            var actingFromArtillery = unit.Position;

            if (unit.Kind == LancerUnitKind.Sniper && artilleryTarget != null && !artilleryTarget.Destroyed)
            {
                // Core Sniper: stay near max range; step in if out of range, step out if too close.
                const int sniperPreferMin = 8;
                var dist = LancerHex.Distance(unit.Position, artilleryTarget.Position);
                LancerGridCoord? step = null;
                if (dist > weapon.Range)
                    step = StepToward(unit, artilleryTarget.Position);
                else if (dist < sniperPreferMin)
                    step = StepAway(unit, artilleryTarget.Position);

                if (step != null && !unit.Immobilized)
                {
                    ReserveAiMove(step);
                    _aiQueue.Add(new AiStep { UnitId = unit.Id, Kind = AiStepKind.Move, TargetCell = step });
                    actingFromArtillery = step;
                }
                else if (unit.Immobilized)
                {
                    unit.Immobilized = false;
                }
            }

            if (artilleryTarget != null && !artilleryTarget.Destroyed
                && CanAttackFrom(actingFromArtillery, artilleryTarget.Position, weapon))
            {
                _aiQueue.Add(new AiStep
                {
                    UnitId = unit.Id,
                    Kind = AiStepKind.Attack,
                    TargetUnitId = artilleryTarget.Id
                });
            }

            return;
        }

        // Mooks / Assaults / Cutlasses: close into preferred engagement band, then attack.
        // Preferring a mid-band (not max rifle range) keeps units stepping inside Tortuga
        // Overwatch threat (3) so Sentinel's second reaction can fire in the same round.
        var preferredTarget = player != null && !player.Destroyed ? player : relay;
        var moveGoal = IsMookKind(unit.Kind) && relay is { Destroyed: false }
            ? relay.Position
            : preferredTarget?.Position;

        var actingFrom = unit.Position;
        // Preferred band 2: keep closing from threat-3 hexes so Tortuga Overwatch
        // (start-inside-threat) can fire; stop once adjacent/near-adjacent.
        const int preferredBand = 2;

        // Hyper-Reflex Immobilize: no move this activation.
        if (unit.Immobilized)
        {
            unit.Immobilized = false;
            AddLog(Loc.GetString("lancer-arcade-log-immobilized-skip",
                ("unit", unit.Kind.ToString())));
        }
        else if (moveGoal != null)
        {
            var dist = LancerHex.Distance(unit.Position, moveGoal);
            if (dist > preferredBand)
            {
                var step = StepToward(unit, moveGoal);
                if (step != null)
                {
                    ReserveAiMove(step);
                    _aiQueue.Add(new AiStep { UnitId = unit.Id, Kind = AiStepKind.Move, TargetCell = step });
                    actingFrom = step;
                }
            }
        }

        if (player != null && !player.Destroyed && CanAttackFrom(actingFrom, player.Position, weapon))
        {
            _aiQueue.Add(new AiStep { UnitId = unit.Id, Kind = AiStepKind.Attack, TargetUnitId = player.Id });
            return;
        }

        if (relay != null && !relay.Destroyed && CanAttackFrom(actingFrom, relay.Position, weapon))
        {
            _aiQueue.Add(new AiStep { UnitId = unit.Id, Kind = AiStepKind.Attack, TargetUnitId = relay.Id });
        }
    }

    private bool CanAttackFrom(LancerGridCoord from, LancerGridCoord to, LancerWeaponDef weapon) =>
        LancerHex.Distance(from, to) <= weapon.Range
        && (weapon.Tags.HasFlag(LancerWeaponTags.Arcing) || HasLineOfSight(from, to));

    private LancerGridCoord? StepToward(UnitRecord unit, LancerGridCoord target)
    {
        var best = (LancerGridCoord?) null;
        var bestDist = int.MaxValue;

        foreach (var next in LancerHex.Neighbors(unit.Position))
        {
            if (!IsValidAiMoveDestination(next))
                continue;

            var dist = LancerHex.Distance(next, target);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = next;
            }
        }

        return best;
    }

    private LancerGridCoord? StepAway(UnitRecord unit, LancerGridCoord threat)
    {
        var best = (LancerGridCoord?) null;
        var bestDist = -1;

        foreach (var next in LancerHex.Neighbors(unit.Position))
        {
            if (!IsValidAiMoveDestination(next))
                continue;

            var dist = LancerHex.Distance(next, threat);
            if (dist > bestDist)
            {
                bestDist = dist;
                best = next;
            }
        }

        return best;
    }

    private bool IsValidAiMoveDestination(LancerGridCoord next) =>
        LancerHex.InBounds(next)
        && !IsOccupied(next)
        && !_reservedAiMoveCells.Contains(next)
        && _cells[next.Y][next.X].Terrain != LancerTerrainType.RubbleHard;

    private void ReserveAiMove(LancerGridCoord cell) => _reservedAiMoveCells.Add(cell);

    private void ExecuteAiStep(AiStep step)
    {
        var unit = GetUnit(step.UnitId);
        if (unit == null || unit.Destroyed || unit.Fleeing)
            return;

        // One reaction budget per enemy activation; reset when execution advances to a new unit.
        if (_executingAiUnitId != unit.Id)
        {
            _executingAiUnitId = unit.Id;
            _reactionUsedThisActivation = false;
        }

        switch (step.Kind)
        {
            case AiStepKind.Move when step.TargetCell != null:
                ExecuteEnemyMove(unit, step.TargetCell);
                break;

            case AiStepKind.Attack:
                ExecuteEnemyAttack(unit, step.TargetUnitId);
                break;
        }
    }

    private void ExecuteEnemyMove(UnitRecord unit, LancerGridCoord destination)
    {
        var player = GetPlayerUnit();
        var from = new LancerGridCoord(unit.Position.X, unit.Position.Y);

        if (player != null && CanOfferOverwatch() && !from.Equals(destination) && !_reactionPromptActive)
        {
            var wasThreat = LancerHex.Distance(from, player.Position) <= _overwatchThreatRange;
            var entersThreat = LancerHex.Distance(destination, player.Position) <= _overwatchThreatRange;
            // Classic Overwatch: must start inside threat (covers leave / move within threat).
            // Vanguard III — Semper Vigilo: also when entering threat from outside.
            var classicTrigger = wasThreat;
            var enterTrigger = _hasVanguard && !wasThreat && entersThreat;
            if (classicTrigger || enterTrigger)
            {
                _reactionUnitId = unit.Id;
                // Enter shots resolve after the step; classic shots resolve before it.
                _overwatchAfterMove = enterTrigger;
                var pendingDest = destination;
                var pendingUnit = unit;
                OfferReaction(LancerReactionType.Overwatch, PendingReactionContext.OverwatchMove, () =>
                {
                    FinishEnemyMove(pendingUnit, pendingDest);
                    RetargetQueuedAttackAfterMove(pendingUnit);
                });
                return;
            }
        }

        FinishEnemyMove(unit, destination);
        RetargetQueuedAttackAfterMove(unit);
    }

    private void RetargetQueuedAttackAfterMove(UnitRecord unit)
    {
        // After moving, ensure a queued attack still has LoS/range from the new position.
        // If not, drop it so the unit doesn't fire illegally.
        for (var i = _aiQueue.Count - 1; i >= 0; i--)
        {
            var step = _aiQueue[i];
            if (step.UnitId != unit.Id || step.Kind != AiStepKind.Attack)
                continue;

            var target = GetUnit(step.TargetUnitId);
            var weapon = GetWeaponDefById(unit.WeaponId);
            if (target == null || target.Destroyed || weapon == null
                || LancerHex.Distance(unit.Position, target.Position) > weapon.Range
                || (!weapon.Tags.HasFlag(LancerWeaponTags.Arcing) && !HasLineOfSight(unit.Position, target.Position)))
            {
                _aiQueue.RemoveAt(i);
            }
        }
    }

    private void FinishEnemyMove(UnitRecord unit, LancerGridCoord destination)
    {
        if (unit.Destroyed || unit.Fleeing)
            return;

        if (!LancerHex.InBounds(destination)
            || IsOccupied(destination)
            || _cells[destination.Y][destination.X].Terrain == LancerTerrainType.RubbleHard)
            return;

        unit.Position = destination;
        _reservedAiMoveCells.Remove(destination);
        AddLog(Loc.GetString("lancer-arcade-log-enemy-move",
            ("unit", unit.Kind.ToString()),
            ("coord", FormatCoord(destination))));
    }

    /// <summary>
    /// Prefer CQB (Cannibal) for Tortuga / Vanguard Overwatch; else melee aux.
    /// </summary>
    private int PickOverwatchWeaponIndex()
    {
        if (_hasVanguard)
        {
            for (var i = 0; i < WeaponSlotCount; i++)
            {
                var def = GetWeaponDef(i);
                if (def != null && IsCqbWeapon(def) && _weaponLoaded[i])
                    return i;
            }
        }

        // Prefer first loaded ranged/CQB slot; else melee Loading weapon if loaded; else aux.
        for (var i = 0; i < WeaponSlotCount; i++)
        {
            var def = GetWeaponDef(i);
            if (def == null || !_weaponLoaded[i])
                continue;
            if (def.Range > 1 || IsCqbWeapon(def))
                return i;
        }

        for (var i = 0; i < WeaponSlotCount; i++)
        {
            var def = GetWeaponDef(i);
            if (def != null && _weaponLoaded[i])
                return i;
        }

        return _overwatchThreatRange > 1 ? 0 : 1;
    }

    private void ExecuteEnemyAttack(UnitRecord attacker, int targetId)
    {
        var target = GetUnit(targetId);
        if (target == null || target.Destroyed)
        {
            target = GetRelay();
            if (target == null || target.Destroyed)
                return;
        }

        var weapon = GetWeaponDefById(attacker.WeaponId);
        if (weapon == null)
            return;

        if (weapon.Tags.HasFlag(LancerWeaponTags.Loading) && !attacker.WeaponLoaded)
            return;

        if (LancerHex.Distance(attacker.Position, target.Position) > weapon.Range)
            return;

        if (!weapon.Tags.HasFlag(LancerWeaponTags.Arcing) && !HasLineOfSight(attacker.Position, target.Position))
            return;

        var stats = GetEnemyStats(attacker.Kind, attacker.Tier, attacker.Veteran);
        BeginAttackResolution(attacker, target, weapon, -1, stats.AccBonus, isPlayerRoll: false);

        if (weapon.Tags.HasFlag(LancerWeaponTags.Loading))
            attacker.WeaponLoaded = false;
    }

    private void OfferReaction(LancerReactionType reaction, PendingReactionContext context, Action? continueAfter)
    {
        if (reaction == LancerReactionType.Overwatch && !CanOfferOverwatch())
        {
            continueAfter?.Invoke();
            return;
        }

        if (reaction == LancerReactionType.Brace && !CanOfferBrace())
        {
            continueAfter?.Invoke();
            return;
        }

        _reactionPromptActive = true;
        _pendingReaction = reaction;
        _reactionTimer = ReactionTimeout;
        _reactionContext = context;
        _pendingReactionContinue = continueAfter;
        _processingAi = false;
        SendReactionPrompt(reaction, ReactionTimeout, context == PendingReactionContext.BraceDamage ? _pendingBraceDamage : 0);
        AddLog(Loc.GetString(reaction == LancerReactionType.Overwatch
            ? "lancer-arcade-log-overwatch-available"
            : "lancer-arcade-log-brace-available",
            ("damage", _pendingBraceDamage)));
        BroadcastState();
    }

    private void ResolveReaction(bool accepted)
    {
        _reactionPromptActive = false;
        var context = _reactionContext;
        var continueAfter = _pendingReactionContinue;
        _pendingReactionContinue = null;
        _reactionContext = PendingReactionContext.None;

        AddLog(Loc.GetString(accepted
            ? "lancer-arcade-log-reaction-accept"
            : "lancer-arcade-log-reaction-decline",
            ("reaction", _pendingReaction.ToString())));

        if (accepted)
        {
            _reactionUsedThisActivation = true;
            if (_pendingReaction == LancerReactionType.Overwatch)
            {
                _overwatchUsesThisRound++;
            }
            else if (_pendingReaction == LancerReactionType.Brace)
            {
                _braceUsedThisRound = true;
                // Core Brace: no further reactions until end of your next turn;
                // next turn is one-quick only; attacks vs you at +1 DIFF until then.
                _braceReactionLockout = true;
                _braceRestrictedPending = true;
                _braceDefenseActive = true;
            }
        }

        if (accepted && context == PendingReactionContext.OverwatchMove)
        {
            var mover = GetUnit(_reactionUnitId);
            var player = GetPlayerUnit();
            var shootAfterMove = _overwatchAfterMove;
            _overwatchAfterMove = false;

            if (mover != null && player != null && !mover.Destroyed)
            {
                // Semper Vigilo enter: complete the step into threat, then shoot from there.
                // Classic: shoot first; continueAfter finishes the move afterward.
                if (shootAfterMove)
                {
                    continueAfter?.Invoke();
                    continueAfter = null;
                }

                var weaponIndex = PickOverwatchWeaponIndex();
                var weapon = GetWeaponDef(weaponIndex);
                if (weapon != null)
                {
                    var dist = LancerHex.Distance(player.Position, mover.Position);
                    // Sentinel trait: +1 ACC on reactions (always on). Grit applies.
                    var accBonus = _playerGrit + (_hasSentinel ? 1 : 0);
                    if (_coreKind == LancerCoreKind.Raijin && _coreActive)
                        accBonus += 1;
                    // Handshake Etiquette applies on Overwatch CQB shots too.
                    if (_hasVanguard && IsCqbWeapon(weapon) && IsVanguardCqbBand(dist))
                        accBonus += 1;
                    if (weapon.AccWithin > 0 && dist <= weapon.AccWithin)
                        accBonus += 1;

                    var consumeLock = mover.LockedOn;
                    if (consumeLock)
                        accBonus += 1;

                    if (_coreKind == LancerCoreKind.TortugaSentinel && _coreActive)
                        _hyperReflexImmobilizeUnitId = mover.Id;

                    BeginAttackResolution(
                        player,
                        mover,
                        weapon,
                        weaponIndex,
                        accBonus: accBonus,
                        isPlayerRoll: true,
                        consumeLockOn: consumeLock,
                        onApplied: () =>
                        {
                            _hyperReflexImmobilizeUnitId = -1;
                            continueAfter?.Invoke();
                            ResumeAiAfterReaction();
                            NotifyTutorialEvent(TutorialAdvanceEvent.OverwatchResolved);
                        });
                    return;
                }
            }
        }
        else
        {
            _overwatchAfterMove = false;
        }

        if (accepted && context == PendingReactionContext.BraceDamage)
        {
            ApplyPendingBrace(braced: true);
            ResumeAiAfterReaction();
            NotifyTutorialEvent(TutorialAdvanceEvent.BraceResolved);
            return;
        }

        if (context == PendingReactionContext.BraceDamage)
        {
            ApplyPendingBrace(braced: false);
            ResumeAiAfterReaction();
            // Declining still completes the lesson so the player is not soft-locked.
            NotifyTutorialEvent(TutorialAdvanceEvent.BraceResolved);
            return;
        }

        continueAfter?.Invoke();
        ResumeAiAfterReaction();

        // Declining Overwatch still advances the tutorial.
        if (!accepted && context == PendingReactionContext.OverwatchMove)
            NotifyTutorialEvent(TutorialAdvanceEvent.OverwatchResolved);
    }

    private void ApplyPendingBrace(bool braced)
    {
        if (_pendingBraceAttacker == null || _pendingBraceTarget == null)
            return;

        var weapon = GetWeaponDefById(_pendingBraceAttacker.WeaponId) ?? Weapons[WeaponGruntRifle];

        ApplyResolvedAttack(
            _pendingBraceAttacker,
            _pendingBraceTarget,
            -1,
            Loc.GetString(weapon.NameLoc),
            _pendingBraceRoll,
            _pendingBraceAcc,
            _pendingBraceDiff,
            _pendingBraceHit,
            _pendingBraceCrit,
            _pendingBraceDamage,
            weapon.Effect,
            _pendingBraceAttacker.Position,
            _pendingBraceTarget.Position,
            braced,
            blastRadius: weapon.BlastRadius,
            blastCenter: _pendingBraceTarget.Position);

        // Brace no longer applies Impaired — Core uses reaction lockout + restricted turn.

        _pendingBraceAttacker = null;
        _pendingBraceTarget = null;
        _pendingBraceDamage = 0;
        _pendingBraceRoll = 0;
        _pendingBraceAcc = 0;
        _pendingBraceDiff = 0;
        _pendingBraceHit = false;
        _pendingBraceCrit = false;
    }

    private void ResumeAiAfterReaction()
    {
        if (_aiQueue.Count > 0)
        {
            _processingAi = true;
            _aiStepTimer = AiStepDelay;
        }
        else if (_phase == LancerGamePhase.Combat)
        {
            _processingAi = false;
            BeginPlayerTurn();
        }

        BroadcastState();
    }
}
