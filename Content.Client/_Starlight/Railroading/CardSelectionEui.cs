using System.Linq;
using System.Numerics;
using Content.Client._Starlight.NewLife;
using Content.Client._Starlight.UI;
using Content.Client.Eui;
using Content.Client.Lobby;
using Content.Shared._Starlight.Railroading;
using Content.Shared.Eui;
using Content.Shared.Starlight.NewLife;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using static Robust.Client.Input.Mouse;
using static Robust.Client.UserInterface.Controls.LayoutContainer;

namespace Content.Client._Starlight.Railroading;

[UsedImplicitly]
public sealed class CardSelectionEui : BaseEui
{
    private List<IDisposable> _disposables = [];
    private static readonly Vector2 _cardSize = new(264, 370);
    private static readonly Vector2 _cardContentSize = new(254, 200);
    private static readonly Vector2 _cardDescSize = new(255, 160);
    private SLWindow _window;

    public CardSelectionEui()
    {
        _window = new SLWindow();
        _window.OnClose += ()=> SendMessage(new CardSelectionClosedMessage());
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase baseState)
    {
        base.HandleState(baseState);

        if (baseState is not CardSelectionEuiState state)
            return;

        var size = new Vector2((_cardSize.X * state.Cards.Count) + (6 * state.Cards.Count), _cardSize.Y);
        _window.Resizable = false;
        _window.Contents.SetSize = size;
        _window.Contents.MinSize = size;
        _window.Contents.MaxSize = size;

        _window.Title = Loc.GetString("card-selection-window-title");

        _window.Contents.RemoveAllChildren();
        _window
            .Box
            (
                BoxContainer.LayoutOrientation.Horizontal,
                box => 
                {
                    box.Align = BoxContainer.AlignMode.Center;
                    state
                    .Cards.ForEach(card => box
                    .Layout(layout =>
                    {
                        layout.FixSize(_cardSize)
                            .WithMargin(new Thickness(3,0));
                        if (card.Image?.TexturePath is not null)
                            layout.TextureRect(textureRect =>
                            {
                                textureRect.Margin = new Thickness(3, 5, 2, 5);
                                textureRect.MaxSize = _cardContentSize;
                                textureRect.TexturePath = card.Image.TexturePath.ToString();
                                textureRect.Stretch = TextureRect.StretchMode.KeepAspect;
                            });
                        layout.Button(
                            button => button
                                .WhenPressed(_ =>
                                {
                                    SendMessage(new CardSelectedMessage() { Card = card.Id });
                                    Closed();
                                })
                                .WhenMouseEntered(_ => button.Modulate(Color.ForestGreen))
                                .WhenMouseExited(_ => button.Modulate(card.Color))
                                .FixSize(_cardSize)
                                .AddClass("CardBorder")
                                .Modulate(card.Color)
                        );
                        layout.Box(BoxContainer.LayoutOrientation.Vertical,
                            cardBox =>
                            {
                                cardBox.MinSize = _cardSize;
                                cardBox.MaxSize = _cardSize;
                                RenderCard(cardBox, card);
                            });

                    }));
                }
            );
    }
    private static void RenderCard(SLBox cardBox, Card card)
    {
        cardBox.Box(BoxContainer.LayoutOrientation.Horizontal,
            box =>
            {
                box.Panel(panel => panel
                    .Label(x => x.WithText(card.Title))
                    .WithHorizontalExp()
                    .WithMargin(new Thickness(5, 5, -1, 0))
                    .WithVAlignment(Control.VAlignment.Top)
                    .AddClass("CardHeader")
                    .Modulate(card.Color));
                if (card.Icon is not null)
                    box.Panel(panel => panel
                        .WithMargin(new Thickness(0, 5, 5, 0))
                        .AddClass("CardBanner")
                        .Modulate(card.Color)
                        .Label(x => x.WithText(card.Icon)
                                    .WithFont("/Fonts/_Starlight/GameIcons/game-icons.ttf", 32)
                                    .WithMargin(new Thickness(-7, 0, -11, -4))
                                    .WithMouseFilter(Control.MouseFilterMode.Pass)
                                    .WhenMouseEntered(_ => panel.Modulate(Color.ForestGreen))
                                    .WhenMouseExited(_ => panel.Modulate(card.Color))
                                    .Modulate(card.IconColor)));
            });
        cardBox.Box(BoxContainer.LayoutOrientation.Horizontal, box =>
        {
            box.Align = BoxContainer.AlignMode.End;
            box.WithMargin(new Thickness(8, 85, 8, 0));
            box.Panel(panel =>
                {
                    panel.AddClass("MenuBar");
                    panel.Modulate(card.Color);

                    if (card.CreditReward is { } creditReward)
                        box.Label(x => x.WithText("")
                                .WithFont("/Fonts/_Starlight/GameIcons/game-icons.ttf", 24)
                                .WithMouseFilter(Control.MouseFilterMode.Pass)
                                .WithTooltip(Loc.GetString("rr-credit-reward", ("Min", creditReward.Min), ("Max", creditReward.Max)))
                                .WhenMouseEntered(_ => x.Modulate(Color.Cyan))
                                .WhenMouseExited(_ => x.Modulate(Color.FromHex("#80FF75"))
                                .Modulate(Color.FromHex("#80FF75"))));

                    if (card.HasSecretAccess)
                        box.Label(x => x.WithText("")
                                .WithFont("/Fonts/_Starlight/GameIcons/game-icons.ttf", 24)
                                .WithMouseFilter(Control.MouseFilterMode.Pass)
                                .WithTooltip(Loc.GetString("rr-secret-access-hint"))
                                .WhenMouseEntered(_ => x.Modulate(Color.Cyan))
                                .WhenMouseExited(_ => x.Modulate(Color.FromHex("#FF75C1"))
                                .Modulate(Color.FromHex("#FF75C1"))));
                });
        });
        cardBox.Panel(panel =>
        {
            panel.AddClass("CardBody")
                .WithMargin(new Thickness(-2, 95, 0, 7))
                .Modulate(card.Color)
                .WithHorizontalExp()
                .WithVAlignment(Control.VAlignment.Bottom);

            // To-do: rework the layout once it becomes clear why the alignment isn’t working.
            panel.MinSize = _cardDescSize;
            panel.MaxSize = _cardDescSize;

            panel.RichText(label => label
                    .WithText($"[font=\"Default\" size=11]{card.Description}[/font]")
                    .WithVAlignment(Control.VAlignment.Top));
        });
    }
}
