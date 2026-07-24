using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Pow3r;
using JetBrains.Annotations;

namespace Content.Server.Power.EntitySystems
{
    /// <summary>
    ///     Implements "backup battery" behaviour: a battery tagged with
    ///     <see cref="PowerNetworkBatteryPriorityComponent"/> only discharges
    ///     once lower-priority batteries on the same network are saturated
    ///     and can no longer meet demand on their own.
    /// </summary>
    /// <remarks>
    ///     Deliberately does not touch <see cref="PowerNetSystem"/> or the
    ///     pow3r solver. It only reads the public runtime state exposed on
    ///     <see cref="PowerNetworkBatteryComponent"/> after the solver has
    ///     run, and flips <see cref="PowerNetworkBatteryComponent.CanDischarge"/>
    ///     accordingly, which the solver will respect on the next tick.
    /// </remarks>
    [UsedImplicitly]
    public sealed class PowerNetworkBatteryPrioritySystem : EntitySystem
    {
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

                if (tiers.Count <= 1)
                    continue; // nothing to arbitrate, only one tier present

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

                            if (shouldEngage != prio.Engaged)
                            {
                                prio.Engaged = shouldEngage;
                                comp.CanDischarge = shouldEngage;
                            }
                        }
                    }

                    // Fold this tier's capacity and charge into the cumulative
                    // totals for the next, higher tier's checks.
                    foreach (var (_, comp, _) in entries)
                    {
                        cumulativeMaxBelow += comp.MaxSupply;
                        cumulativeStorageBelow += comp.CurrentStorage;
                        cumulativeCapacityBelow += comp.Capacity;
                    }

                    first = false;
                }
            }
        }
    }
}
