// Plushie interaction system - handles cuddle messages for plushies
using Content.Shared._Starlight.Plushies;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Plushies;

/// <summary>
/// Server-side system that handles cuddle messages when using plushies in hand.
/// </summary>
public sealed class PlushieSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CuddleMessageComponent, UseInHandEvent>(OnUseInHand);
    }

    /// <summary>
    /// Shows a localized cuddle message when the plushie is used in hand.
    /// </summary>
    private void OnUseInHand(Entity<CuddleMessageComponent> entity, ref UseInHandEvent args)
    {
        if (string.IsNullOrEmpty(entity.Comp.LocalizedMessageKey))
            return;

        var message = Loc.GetString(entity.Comp.LocalizedMessageKey, ("user", args.User));
        
        _popup.PopupEntity(message, entity, args.User);
        
        args.Handled = true;
    }
}
