using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mousetrap; // Starlight
using Content.Shared.Popups; // Starlight
using Content.Shared.StepTrigger.Components;

namespace Content.Shared.StepTrigger.Systems;

public sealed class StepTriggerImmuneSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!; // Starlight

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

            // Starlight-start: show popup when entity avoids mousetrap
            if (HasComp<MousetrapComponent>(ent))
            {
                _popup.PopupEntity(Loc.GetString("mousetrap-avoided", ("entity", args.Tripper)), ent);
            }
            // Starlight-end
        }
    }

    private void OnExamined(EntityUid uid, PreventableStepTriggerComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("clothing-required-step-trigger-examine"));
    }
}
