using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Cards.Events;
using Content.Shared._Moffstation.Cards.Systems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Moffstation.Cards;

[UsedImplicitly]
public sealed class PlayingCardHandMenuBoundUserInterface(
    EntityUid owner,
    Enum uiKey
) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        Update();
        _menu.OpenOverMouseScreenPosition();
    }

    public override void Update()
    {
        _menu?.SetButtons(
            EntMan.System<SharedPlayingCardsSystem>()
                .GetCards(Owner)
                .Select(card => new RadialMenuActionOption<Entity<PlayingCardComponent>>(OnPressed, card)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(card),
                    ToolTip = card.Comp.Name(),
                })
        );
    }

    private void OnPressed(Entity<PlayingCardComponent> card)
    {
        SendPredictedMessage(new DrawPlayingCardFromHandMessage(EntMan.GetNetEntity(card)));
        _menu?.Close();
        Close();
    }
}
