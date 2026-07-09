using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Cards.Systems;
using Robust.Client.GameObjects;

namespace Content.Client._Moffstation.Cards;

public sealed partial class PlayingCardsSystem : SharedPlayingCardsSystem
{
    [Dependency] private AppearanceSystem _appearance = default!;

    // Starlight edit Start: Expression body
    protected override void ForceAppearanceUpdate(Entity<PlayingCardComponent> card) =>
        _appearance.OnChangeData(card, CompOrNull<SpriteComponent>(card));
    // Starlight edit End: Expression body
}
