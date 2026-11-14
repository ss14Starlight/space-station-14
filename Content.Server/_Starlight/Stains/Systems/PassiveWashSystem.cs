using Content.Server._Funkystation.Stains;
using Content.Server._Starlight.Stains.Components;
using Content.Shared._Funkystation.Footprints;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;

namespace Content.Server._Starlight.Stains.Systems;

public sealed partial class PassiveWashSystem : EntitySystem
{
    [Dependency] private StainSystem _stains = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly Dictionary<WashExposureKey, float> _washExposure = new();
    private readonly HashSet<WashExposureKey> _currentlyWashing = new();
    private readonly HashSet<WashExposureKey> _leftSurfaces = new();
    private const float UpdateInterval = 0.5f;
    private float _updateAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PassiveWashComponent, StepTriggerAttemptEvent>(OnStepAttempt);
    }

    private void OnStepAttempt(Entity<PassiveWashComponent> ent, ref StepTriggerAttemptEvent args) => args.Continue |=
            (ent.Comp.WashSlots != SlotFlags.NONE && HasComp<InventoryComponent>(args.Tripper)) ||
            (ent.Comp.WashFootprints && HasComp<FootprintOwnerComponent>(args.Tripper));

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        var elapsed = MathF.Floor(_updateAccumulator / UpdateInterval) * UpdateInterval;
        _updateAccumulator -= elapsed;

        _currentlyWashing.Clear();

        var query = EntityQueryEnumerator<PassiveWashComponent, StepTriggerComponent, StepTriggerActiveComponent>();
        while (query.MoveNext(out _, out var wash, out var trigger, out _))
        {
            if (wash.WashInterval <= 0f || wash.WashAmount <= 0 ||
                (wash.WashSlots == SlotFlags.NONE && !wash.WashFootprints))
            {
                continue;
            }

            foreach (var wearer in trigger.CurrentlySteppedOn)
            {
                var key = new WashExposureKey(wearer, wash.WashInterval, wash.WashAmount, wash.WashSlots, wash.WashFootprints);

                // Don't double clean when standing on 2 of the exact same washers
                if (!_currentlyWashing.Add(key))
                    continue;

                var amount = AccumulateExposure(key, elapsed);
                if (amount > 0)
                    Wash(wearer, wash, amount);
            }
        }

        _leftSurfaces.Clear();
        foreach (var key in _washExposure.Keys)
        {
            if (!_currentlyWashing.Contains(key))
                _leftSurfaces.Add(key);
        }

        foreach (var key in _leftSurfaces)
            _washExposure.Remove(key);
    }

    private FixedPoint2 AccumulateExposure(WashExposureKey key, float elapsed)
    {
        var exposure = _washExposure.GetValueOrDefault(key) + elapsed;
        var ticks = (int) MathF.Floor(exposure / key.Interval);
        _washExposure[key] = exposure - (ticks * key.Interval);
        return key.Amount * ticks;
    }

    private void Wash(EntityUid wearer, PassiveWashComponent wash, FixedPoint2 amount)
    {
        if (wash.WashSlots != SlotFlags.NONE)
            _stains.CleanEquippedClothing(wearer, amount, wash.WashSlots);

        if (!wash.WashFootprints || !HasComp<FootprintOwnerComponent>(wearer) ||
            !_solutions.TryGetSolution(wearer, FootprintComponent.SolutionName, out var residue, out var contents) ||
            contents.Volume <= 0)
        {
            return;
        }

        _solutions.SplitSolution(residue.Value, amount);
    }

    // Progress carries across matching surfaces, but not between different washing settings. More useful when you're in water, since that tends to last a couple tiles.
    private readonly record struct WashExposureKey(
        EntityUid Wearer,
        float Interval,
        FixedPoint2 Amount,
        SlotFlags Slots,
        bool Footprints);
}
