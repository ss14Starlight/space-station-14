using Content.Shared._Starlight.Admeme.DestinyDice;
using Content.Shared.Dice;
using Robust.Client.GameObjects;

namespace Content.Client.Dice;

public sealed class DiceSystem : SharedDiceSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiceComponent, AfterAutoHandleStateEvent>(OnDiceAfterHandleState);
    }

    private void OnDiceAfterHandleState(Entity<DiceComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;
        
        // SL start | handle destiny dice since their sprite layers are handled differently
        if (HasComp<DestinyDiceComponent>(entity))
        {
            var leftNum = entity.Comp.CurrentValue / 10;
            var rightNum = entity.Comp.CurrentValue % 10;
            _sprite.LayerSetRsiState((entity.Owner, sprite), 2, $"l{leftNum}");
            _sprite.LayerSetRsiState((entity.Owner, sprite), 3, $"r{rightNum}");
            return;
        }
        // SL end

        // TODO maybe just move each die to its own RSI?
        var state = _sprite.LayerGetRsiState((entity.Owner, sprite), 0).Name;
        if (state == null)
            return;

        var prefix = state.Substring(0, state.IndexOf('_'));
        _sprite.LayerSetRsiState((entity.Owner, sprite), 0, $"{prefix}_{entity.Comp.CurrentValue}");
    }
}
