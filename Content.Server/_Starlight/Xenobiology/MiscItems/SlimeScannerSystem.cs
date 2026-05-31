using Content.Server.Chat.Managers;
using Content.Shared._Starlight.Xenobiology;
using Content.Shared._Starlight.Xenobiology.MiscItems;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Xenobiology.MiscItems;

public sealed class SlimeScannerSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<XenobiologyConsoleComponent, ConsoleMsgToScannerEvent>(OnConsoleMsgToScanner);
    }

    private void OnAfterInteract(Entity<SlimeScannerComponent> entity, ref AfterInteractEvent args)
    {
        if (!_entityManager.TryGetComponent<ActorComponent>(args.User, out var actor)) return;
        if (!_entityManager.TryGetComponent<SlimeComponent>(args.Target, out var slime)) return;
        var metaData = MetaData(args.Target.Value);
        if (!_entityManager.TryGetComponent<HungerComponent>(args.Target, out var hunger)) return;

        SendInformation(actor, slime, metaData, hunger);
        RaiseNetworkEvent(new SlimeScannerSoundMessage()
        {
            Owner = GetNetEntity(entity.Owner, MetaData(entity.Owner)),
            User = GetNetEntity(args.User, MetaData(args.User)),
        });
    }

    private void OnConsoleMsgToScanner(Entity<XenobiologyConsoleComponent> entity, ref ConsoleMsgToScannerEvent args)
    {
        if (!_entityManager.TryGetComponent<ActorComponent>(args.User, out var actor)) return;
        if (!_entityManager.TryGetComponent<SlimeComponent>(args.Target, out var slime)) return;
        var metaData = MetaData(args.Target);
        if (!_entityManager.TryGetComponent<HungerComponent>(args.Target, out var hunger)) return;

        SendInformation(actor, slime, metaData, hunger);

        args.Handled = true;
    }

    private void SendInformation(ActorComponent actor, SlimeComponent slime, MetaDataComponent metaData, HungerComponent hunger)
    {
        var channel = actor.PlayerSession.Channel;
        var name = metaData.EntityName;
        var nutrition = FixedPoint2.New(_hungerSystem.GetHunger(hunger));
        var message = $"Name:\t[Bold]{name}[/Bold]\nNutrition:\t[Bold]{nutrition}[/Bold]\nMutation Chance:\t[Bold]{slime.MutationChance * 100F}%[/Bold]";
        _chatManager.ChatMessageToOne(ChatChannel.Local, message, message, EntityUid.Invalid, false, channel);
    }
}
