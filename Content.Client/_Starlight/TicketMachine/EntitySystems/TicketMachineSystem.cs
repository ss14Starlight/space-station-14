using Content.Shared._Starlight.TicketMachine.EntitySystems;
using Content.Shared._Starlight.TicketMachine.Components;
using Content.Shared._Starlight.TicketMachine;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.TicketMachine.EntitySystems;

public sealed class TicketMachineSystem : SharedTicketMachineSystem
{
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TicketMachineComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<TicketComponent, AppearanceChangeEvent>(OnTicketAppearanceChange);
    }

    private void OnTicketAppearanceChange(EntityUid uid, TicketComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<int?>(uid, TicketVisuals.Number, out var number) || !number.HasValue)
            return;

        if (_spriteSystem.LayerMapTryGet(uid, TicketVisualLayers.Number3, out var number3, true))
            _spriteSystem.LayerSetRsiState(uid, number3, component.NumberStateTag + $"{number.Value / 100 % 10}");

        if (_spriteSystem.LayerMapTryGet(uid, TicketVisualLayers.Number2, out var number2, true))
            _spriteSystem.LayerSetRsiState(uid, number2, component.NumberStateTag + $"{number.Value / 10 % 10}");

        if (_spriteSystem.LayerMapTryGet(uid, TicketVisualLayers.Number1, out var number1, true))
            _spriteSystem.LayerSetRsiState(uid, number1, component.NumberStateTag + $"{number.Value % 10}");
    }

    private void OnAppearanceChange(EntityUid uid, TicketMachineComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_spriteSystem.LayerMapTryGet(uid, TicketMachineVisualLayers.Paper, out var paperLayer, true))
        {
            if (_appearance.TryGetData<bool>(uid, TicketMachineVisuals.isFilled, out var isFilled) && !isFilled)
                _spriteSystem.LayerSetRsiState(uid, paperLayer, component.paperStateTag + "empty");
            else if (_appearance.TryGetData<int>(uid, TicketMachineVisuals.Paper, out var paperAmount))
                _spriteSystem.LayerSetRsiState(uid, paperLayer, component.paperStateTag + paperAmount);
        }

        if (_appearance.TryGetData<int>(uid, TicketMachineVisuals.DisplayNumber, out var displayNumber))
        {
            var ticketNumber = Math.Clamp(displayNumber, 0, 999);
            if (_spriteSystem.LayerMapTryGet(uid, TicketMachineVisualLayers.Display3, out var display3, true))
            {
                if (ticketNumber >= 100)
                {
                    _spriteSystem.LayerSetVisible(uid, display3, true);
                    _spriteSystem.LayerSetRsiState(uid, display3, component.displayStateTag + $"{ticketNumber / 100 % 10}");
                }
                else
                    _spriteSystem.LayerSetVisible(uid, display3, false);
            }
            if (_spriteSystem.LayerMapTryGet(uid, TicketMachineVisualLayers.Display2, out var display2, true))
            {
                if (ticketNumber >= 10)
                {
                    _spriteSystem.LayerSetVisible(uid, display2, true);
                    _spriteSystem.LayerSetRsiState(uid, display2, component.displayStateTag + $"{ticketNumber / 10 % 10}");
                }
                else 
                    _spriteSystem.LayerSetVisible(uid, display2, false);
            }
            if (_spriteSystem.LayerMapTryGet(uid, TicketMachineVisualLayers.Display1, out var display1, true))
                _spriteSystem.LayerSetRsiState(uid, display1, component.displayStateTag + $"{ticketNumber % 10}");
        }
    }
}