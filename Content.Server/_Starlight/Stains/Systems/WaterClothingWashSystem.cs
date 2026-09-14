using Content.Server._Funkystation.Stains;
using Content.Server._Starlight.Stains.Components;
using Content.Shared.StepTrigger.Components;

namespace Content.Server._Starlight.Stains.Systems;

public sealed partial class WaterClothingWashSystem : EntitySystem
{
    [Dependency] private StainSystem _stains = default!;

    private readonly Dictionary<EntityUid, float> _waterExposure = new();
    private readonly HashSet<EntityUid> _currentlyInWater = new();
    private readonly HashSet<EntityUid> _leftWater = new();
    private const float UpdateInterval = 0.5f;
    private float _updateAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        var elapsed = MathF.Floor(_updateAccumulator / UpdateInterval) * UpdateInterval;
        _updateAccumulator -= elapsed;

        _currentlyInWater.Clear();

        var query = EntityQueryEnumerator<WaterClothingWashComponent, StepTriggerComponent, StepTriggerActiveComponent>();
        while (query.MoveNext(out _, out var wash, out var trigger, out _))
        {
            if (wash.WashInterval <= 0f || wash.WashAmount <= 0)
                continue;

            foreach (var wearer in trigger.CurrentlySteppedOn)
            {
                // An entity can overlap more than one water tile, but its exposure should only advance once per update.
                if (!_currentlyInWater.Add(wearer))
                    continue;

                var exposure = _waterExposure.GetValueOrDefault(wearer) + elapsed;
                if (exposure < wash.WashInterval)
                {
                    _waterExposure[wearer] = exposure;
                    continue;
                }

                var ticks = (int) MathF.Floor(exposure / wash.WashInterval);
                _stains.CleanEquippedClothing(wearer, amount: wash.WashAmount * ticks);
                _waterExposure[wearer] = exposure - (ticks * wash.WashInterval);
            }
        }

        _leftWater.Clear();
        foreach (var wearer in _waterExposure.Keys)
        {
            if (!_currentlyInWater.Contains(wearer))
                _leftWater.Add(wearer);
        }

        foreach (var wearer in _leftWater)
        {
            _waterExposure.Remove(wearer);
        }
    }
}
