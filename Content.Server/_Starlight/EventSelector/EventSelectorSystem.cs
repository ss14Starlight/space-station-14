using Content.Server.GameTicking;
using Content.Shared._Starlight.EventSelector;
using Content.Shared.Charges.Systems;
using Content.Shared.Timing;

namespace Content.Server._Starlight.EventSelector;

public sealed class EventSelectorSystem : SharedEventSelectorSystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EventSelectorRadialMenuComponent, EventSelectorOnRadialMenuSelectMessage>(OnRadialMenuSelect);
    }

    private void OnRadialMenuSelect(Entity<EventSelectorRadialMenuComponent> entity, ref EventSelectorOnRadialMenuSelectMessage args)
    {
        if (args.Index < 0 || args.Index >= entity.Comp.RadialMenuEntries.Count)
        {
            Log.Error($"{ToPrettyString(args.Actor)} tried to select radial menu index that is out of range for {ToPrettyString(entity)}.");
            return;
        }

        if (!CanActivate(entity, out _))
            return;

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        var selected = entity.Comp.RadialMenuEntries[args.Index];

        var rule = _ticker.AddGameRule(selected.GameRule);
        _ticker.StartGameRule(rule);

        _charges.TryUseCharge(entity.Owner);

        if (!TryComp<UseDelayComponent>(entity.Owner, out var useDelayComp))
            return;

        _useDelay.SetLength((entity.Owner, useDelayComp), selected.UseDelay, _delayId);
        _useDelay.TryResetDelay((entity.Owner, useDelayComp), false, _delayId);
    }
}
