using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Pow3r;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Power.Pow3r
{
    /// <summary>
    ///     Implements "backup battery" behaviour: a battery tagged with
    ///     <see cref="PowerNetworkBatteryPriorityComponent"/> only discharges
    ///     once lower-priority batteries on the same network are saturated
    ///     and can no longer meet demand on their own.
    /// </summary>
    /// <remarks>
    ///     Deliberately does not touch <see cref="PowerNetSystem"/>, the
    ///     pow3r solver, <see cref="PowerNetworkBatteryComponent"/>, or
    ///     <see cref="BatteryComponent"/>. It only reads public runtime
    ///     state/methods already exposed on those components (including
    ///     <see cref="SharedBatterySystem.GetCharge"/> for current charge)
    ///     after the solver has run, and flips
    ///     <see cref="PowerNetworkBatteryComponent.CanDischarge"/> accordingly,
    ///     which the solver will respect on the next tick.
    /// </remarks>
    [UsedImplicitly]
    public sealed class PowerNetworkBatteryPrioritySystem : EntitySystem
    {
        // Starlight Start
        // BatteryComponent deliberately doesn't store a continuously-updated
        // "current charge" field (to avoid re-networking every SMES every
        // tick) - current charge has to be computed via GetCharge instead.
        [Dependency] private readonly SharedBatterySystem _battery = default!;
        // Starlight End

        public override void Initialize()
        {
            base.Initialize();

            // Run after the power net has solved for this tick so we're
            // reacting to fresh CurrentSupply/MaxSupply numbers.
            UpdatesAfter.Add(typeof(PowerNetSystem));
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Group every battery on the map by the network it discharges into.
            // Starlight Start
            var byNetwork = new Dictionary<PowerState.NodeId, List<(EntityUid Uid, PowerNetworkBatteryComponent Comp)>>();
            // Starlight End

            var enumerator = EntityQueryEnumerator<PowerNetworkBatteryComponent>();
            while (enumerator.MoveNext(out var uid, out var comp))
            {
                var netId = comp.NetworkBattery.LinkedNetworkDischarging;
                if (netId == default)
                    continue; // not wired up to a discharge network right now

                if (!byNetwork.TryGetValue(netId, out var list))
                {
                    list = new List<(EntityUid, PowerNetworkBatteryComponent)>();
                    byNetwork[netId] = list;
                }

                list.Add((uid, comp));
            }

            var priorityQuery = GetEntityQuery<PowerNetworkBatteryPriorityComponent>();
            // Starlight Start
            // Read charge from the entity's existing BatteryComponent rather
            // than adding new fields to PowerNetworkBatteryComponent - it's
            // already public and already holds this exact data (the same
            // CurrentCharge/MaxCharge configured via `battery:` in YAML), so
            // no core file needs to change at all.
            var batteryQuery = GetEntityQuery<BatteryComponent>();
            // Starlight End

            foreach (var (_, batteries) in byNetwork)
            {
                // Bucket this network's batteries by priority tier.
                // Untagged batteries are implicit tier 0.
                var tiers = new SortedDictionary<int, List<(EntityUid Uid, PowerNetworkBatteryComponent Comp, PowerNetworkBatteryPriorityComponent? Prio)>>();

                foreach (var (uid, comp) in batteries)
                {
                    priorityQuery.TryGetComponent(uid, out var prio);
                    var tier = prio?.Priority ?? 0;

                    if (!tiers.TryGetValue(tier, out var list))
                    {
                        list = new List<(EntityUid, PowerNetworkBatteryComponent, PowerNetworkBatteryPriorityComponent?)>();
                        tiers[tier] = list;
                    }

                    list.Add((uid, comp, prio));
                }

                // Starlight Start
                // Do NOT early-out on a single tier. A network can end up
                // containing only tagged batteries (e.g. two backups wired
                // together with no untagged primary at all) - in that case
                // the lowest priority actually present still needs to be
                // forced eligible below, since nothing else will ever flip
                // it on from CanDischarge: false.
                // Starlight End

                // The solver already computes exactly the number we want:
                // PowerNetworkBatteryComponent.LoadingNetworkDemand is set by
                // BatteryRampPegSolver to the network's unmet demand (after
                // ordinary generators, before battery help), identically on
                // every discharging battery in the network, and reset to 0
                // when there's no shortfall. Read it from tier 0 specifically
                // - a currently-disengaged higher tier has a stale/zeroed
                // copy of this value, since the solver skips batteries with
                // CanDischarge == false when assigning it.
                var tier0 = tiers[tiers.Keys.Min()];
                float totalDemand = 0f;
                foreach (var (_, comp, _) in tier0)
                    totalDemand = Math.Max(totalDemand, comp.LoadingNetworkDemand);

                // Walk tiers from lowest (primary) to highest (backup-most),
                // tracking the combined MAX CAPACITY and combined stored
                // charge of everything strictly below the current tier.
                float cumulativeMaxBelow = 0f;
                float cumulativeStorageBelow = 0f;
                float cumulativeCapacityBelow = 0f;
                var first = true;

                foreach (var (_, entries) in tiers)
                {
                    if (!first)
                    {
                        // Rate signal: is demand outrunning what the lower
                        // tiers are physically able to output right now?
                        var rateSaturation = cumulativeMaxBelow > 0f ? totalDemand / cumulativeMaxBelow : 0f;

                        // Charge signal: are the lower tiers running dry,
                        // independent of whether they're currently rate-capped?
                        var chargeFraction = cumulativeCapacityBelow > 0f
                            ? cumulativeStorageBelow / cumulativeCapacityBelow
                            : 0f;

                        foreach (var (uid, comp, prio) in entries)
                        {
                            if (prio == null)
                                continue; // shouldn't happen for tier > 0, but be safe

                            var overloaded = prio.Engaged
                                ? rateSaturation > prio.DisengageThreshold
                                : rateSaturation >= prio.EngageThreshold;

                            var depleted = prio.Engaged
                                ? chargeFraction < prio.ChargeDisengageThreshold
                                : chargeFraction <= prio.ChargeEngageThreshold;

                            // Engage on either condition; only disengage once
                            // BOTH have cleared, so a lower tier that's either
                            // overloaded OR running dry keeps the backup on.
                            var shouldEngage = overloaded || depleted;

                            // Starlight Start
                            // Always assign CanDischarge, not just on an
                            // Engaged transition - otherwise a later external
                            // write to CanDischarge (or a component that only
                            // just started being tracked) is never corrected
                            // back to the state this system actually wants.
                            comp.CanDischarge = shouldEngage;
                            if (shouldEngage != prio.Engaged)
                                prio.Engaged = shouldEngage;
                            // Starlight End
                        }
                    }
                    // Starlight Start
                    else
                    {
                        // This is the lowest priority tier actually present
                        // on this network. There's nothing below it to defer
                        // to, so it must always be eligible to discharge -
                        // otherwise a network made up entirely of tagged
                        // batteries (no untagged primary) would leave this
                        // tier stuck at its initial CanDischarge: false
                        // forever, since only non-lowest tiers are gated
                        // above. Untagged (Prio == null) entries are left
                        // alone - they were never ours to manage.
                        foreach (var (_, comp, prio) in entries)
                        {
                            if (prio == null)
                                continue;

                            comp.CanDischarge = true;
                            if (!prio.Engaged)
                                prio.Engaged = true;
                        }
                    }
                    // Starlight End

                    // Starlight Start
                    // Fold this tier's max supply into the cumulative rate
                    // total, and its charge into the cumulative charge total.
                    // MaxCharge is a plain public field, safe to read directly.
                    // Current charge is NOT a stored field on BatteryComponent
                    // (this fork computes it on demand to avoid networking
                    // every battery every tick) - GetCharge derives it from
                    // LastCharge/ChargeRate/LastUpdate.
                    foreach (var (uid, comp, _) in entries)
                    {
                        cumulativeMaxBelow += comp.MaxSupply;

                        if (batteryQuery.TryGetComponent(uid, out var battery))
                        {
                            Entity<BatteryComponent> batteryEnt = (uid, battery);
                            cumulativeStorageBelow += _battery.GetCharge(batteryEnt.AsNullable());
                            cumulativeCapacityBelow += battery.MaxCharge;
                        }
                    }
                    // Starlight End

                    first = false;
                }
            }
        }
    }
}
