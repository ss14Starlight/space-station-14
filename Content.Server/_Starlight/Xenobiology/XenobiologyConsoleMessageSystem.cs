using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared._Starlight.Xenobiology;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Xenobiology;

public sealed partial class XenobiologyConsoleMessageSystem : EntitySystem
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsoleTextMsgEvent>(OnConsoleTextMsg);
        SubscribeLocalEvent<ConsolePopupEvent>(OnConsolePopup);
    }

    private void OnConsoleTextMsg(ConsoleTextMsgEvent args)
    {
        if (!_entityManager.TryGetComponent<ActorComponent>(args.User, out var actor)) return;

        var channel = actor.PlayerSession.Channel;
        _chatManager.ChatMessageToOne(ChatChannel.Local, args.Message.ToString(), args.Message.ToString(), EntityUid.Invalid, false, channel);
    }

    private void OnConsolePopup(ConsolePopupEvent args) => _popupSystem.PopupEntity(args.Message.ToString(), args.Console, PopupType.Small);
}
