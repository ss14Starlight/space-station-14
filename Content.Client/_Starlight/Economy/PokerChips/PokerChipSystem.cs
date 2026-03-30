using System.Linq;
using Content.Shared._Starlight.Economy.PokerChips.Components;
using Content.Shared._Starlight.Economy.PokerChips.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Economy.PokerChips;

public sealed class PokerChipSystem : SharedPokerChipSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PokerChipComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PokerChipComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    protected override void ForceAppearanceUpdate(Entity<PokerChipComponent> chip) =>
        _appearance.OnChangeData(chip, CompOrNull<SpriteComponent>(chip));

    private void OnStartup(Entity<PokerChipComponent> chip, ref ComponentStartup ev)
    {
        if (!TryComp<SpriteComponent>(chip, out var sprite))
            return;
        var ent = (Entity<SpriteComponent>)(chip, sprite);

        var valueLayer = _sprite.AddBlankLayer(ent);
        // i fucking hate sprite system why is index internal
        int? vLayerIdx = null;
        foreach (var (idx, layer) in sprite.AllLayers.Index())
        {
            if (layer != valueLayer) continue;
            vLayerIdx = idx;
            break;
        }
        if (vLayerIdx is null) return;

        // >ent.AsNullable() god this engine is stupid :sob:
        _sprite.LayerMapAdd(ent.AsNullable(), chip.Comp.ValueLayerKey, vLayerIdx.Value);
        UpdateSprite(chip);
    }

    private void OnAfterAutoHandleState(Entity<PokerChipComponent> chip, ref AfterAutoHandleStateEvent ev) =>
        UpdateSprite(chip);

    private void UpdateSprite(Entity<PokerChipComponent> chip)
    {
        if (!TryComp<SpriteComponent>(chip, out var sprite))
            return;

        _sprite.LayerSetRsi((chip, sprite), chip.Comp.ValueLayerKey, new ResPath("_Starlight/Objects/Economy/poker_chip.rsi"),
            new RSI.StateId($"{chip.Comp.ValueStatePrefix}{chip.Comp.ChipValueType.ToString().ToLower()}"));
    }
}
