using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mousetrap;
using Content.Shared.StepTrigger.Components;

namespace Content.Shared.StepTrigger.Systems;

public sealed class StepTriggerImmuneSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PreventableStepTriggerComponent, StepTriggerAttemptEvent>(OnStepTriggerClothingAttempt);
        SubscribeLocalEvent<PreventableStepTriggerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStepTriggerClothingAttempt(Entity<PreventableStepTriggerComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (HasComp<ProtectedFromStepTriggersComponent>(args.Tripper) || _inventory.TryGetInventoryEntity<ProtectedFromStepTriggersComponent>(args.Tripper, out _))
        {
            args.Cancelled = true;

            // Starlight-start: raise event when entity avoids mousetrap
            if (HasComp<MousetrapComponent>(ent))
            {
                var ev = new MousetrapAvoidedEvent(args.Tripper);
                RaiseLocalEvent(ent, ref ev);
            }
            // Starlight-end
        }
    }

    private void OnExamined(EntityUid uid, PreventableStepTriggerComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("clothing-required-step-trigger-examine"));
    }
}

// Starlight-start: event for when entity avoids mousetrap
[ByRefEvent]
public record struct MousetrapAvoidedEvent(EntityUid Tripper);
// Starlight-end
