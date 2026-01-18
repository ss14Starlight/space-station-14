using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.PlayingCards.Card;

public sealed class PlayingCardSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayingCardComponent, GetVerbsEvent<AlternativeVerb>>(AddTurnOnVerb);
        SubscribeLocalEvent<PlayingCardComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PlayingCardComponent, UseInHandEvent>(OnUse);
    }

    private void OnExamined(EntityUid uid, PlayingCardComponent component, ExaminedEvent args)
    {
        if (args.IsInDetailsRange && !component.Flipped)
        {
            args.PushMarkup(Loc.GetString("card-examined",
                ("target", Loc.GetString($"card-sc-{Enum.GetName(component.Suit)?.ToLower()}-{component.Value}"))));
        }
    }
    
    private void AddTurnOnVerb(EntityUid uid, PlayingCardComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => FlipCard(uid, component),
            Text = Loc.GetString("cards-verb-flip"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
            Priority = 1
        });
    }
    
    private void OnUse(EntityUid uid, PlayingCardComponent comp, UseInHandEvent args)
    {
        if (_net.IsClient)
            return;
        if (args.Handled)
            return;
        FlipCard(uid, comp);
        args.Handled = true;
    }
    
    private void FlipCard(EntityUid uid, PlayingCardComponent component)
    {
        if (_net.IsClient)
            return;
        component.Flipped = !component.Flipped;
        Dirty(uid, component);
        RaiseNetworkEvent(new PlayingCardFlipUpdatedEvent(GetNetEntity(uid)));
    }
}