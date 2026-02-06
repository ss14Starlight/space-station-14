using Content.Server.Chat.Managers;
using Content.Server.Silicons.StationAi;
using Content.Shared._Starlight.Xenobiology.MiscItems;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.StationAi;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Xenobiology.MiscItems;

public sealed class XenobiologyConsoleCameraTaggerSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly StationAiSystem _stationAiSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobiologyConsoleCameraTaggerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<XenobiologyConsoleCameraTaggerComponent> entity,
        ref AfterInteractEvent args)
    {
        if (!_entityManager.TryGetComponent<StationAiVisionComponent>(args.Target, out var stationAiVisionComponent))
            return;
        if (!_stationAiSystem.AddTag(stationAiVisionComponent, "xenobiology")) return;
        if (!_entityManager.TryGetComponent<ActorComponent>(args.User, out var actorComponent)) return;
        
        var channel = actorComponent.PlayerSession.Channel;
        var message = "Connected camera to the Xenobiology Console network.";
        _chatManager.ChatMessageToOne(ChatChannel.Local, message, message, EntityUid.Invalid, false, channel);
        Dirty(args.Target.Value, stationAiVisionComponent);
    }
}