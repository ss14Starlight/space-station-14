using Content.Shared._Starlight.SocialInteraction.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.SocialInteraction.Systems;

public sealed partial class SocialInteractionSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private SharedChatSystem _chatSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        //subscribe to inspect events on the physical social interaction receiver component
        SubscribeLocalEvent<SocialInteractionReceiverComponent, GetVerbsEvent<Verb>>(AddSocialInteractionVerbs);
    }

    private void AddSocialInteractionVerbs(EntityUid uid, SocialInteractionReceiverComponent component, GetVerbsEvent<Verb> args)
    {
        //check if the user also has a interaction giver
        if (!HasComp<SocialInteractionGiverComponent>(args.User))
            return;

        //create a verb subcategory
        var category = new VerbCategory("social-interaction-component-verb", null);

        //enumerate all the physical social interaction prototypes
        foreach (var protoid in component.InteractionPrototypes)
        {
            //resolve the proto itself
            if (!_protoMan.TryIndex<SocialInteractionPrototype>(protoid, out var proto))
                continue;

            // check if interaction needs physical contact
            if (proto.IsPhysical && (!CheckInteractable(args.User, args.Target)))
                continue;

            // check if this interaction allows self-targeting
            if (!proto.AllowSelfTarget && args.User == args.Target)
                continue;

            //make a verb for each one
            Verb verb = new()
            {
                Text = Loc.GetString(proto.VerbName),
                Category = category,
                Act = () => InteractionPopupAction(uid, args, proto)
            };

            args.Verbs.Add(verb);
        }
    }

    private bool CheckInteractable(EntityUid user, EntityUid target)
    {
        if (!_actionBlockerSystem.CanInteract(user, target))
            return false;

        if (!_interactionSystem.InRangeUnobstructed(user, target))
            return false;

        return true;
    }

    private void InteractionPopupAction(EntityUid uid, GetVerbsEvent<Verb> args, SocialInteractionPrototype proto)
    {
        // needed to not play interaction audio multiple times
        if (!_timing.IsFirstTimePredicted)
            return;

        // check if interaction needs physical contact
        if (proto.IsPhysical && !CheckInteractable(args.User, args.Target))
            return;

        var selfTarget = args.User == args.Target; // whether or not we're interacting with ourselves
        var msg = ""; // Stores the text to be shown in the popup message
        SoundSpecifier? sfx = null; // Stores the filepath of the sound to be played

        if (proto.InteractString != null)
            msg = Loc.GetString(proto.InteractString, ("target", Identity.Entity(args.Target, EntityManager)));

        if (proto.InteractSound != null)
            sfx = proto.InteractSound;

        // pop-up message for the target - skip if self targeted
        if (!selfTarget && proto.MessagePerceivedByOthers is { } message)
        {
            var msgOthers = Loc.GetString(message,
                ("user", Identity.Entity(args.User, EntityManager)),
                ("target", Identity.Entity(args.Target, EntityManager)));

            _popupSystem.PopupEntity(msgOthers, uid, Filter.PvsExcept(args.User, entityManager: EntityManager), true);
        }

        // emote message for chat
        if (proto.EmoteMessage is { } emoteMessage)
        {
            // show a different message if we're using the emote on ourselves
            var emoteTarget = selfTarget
                ? proto.EmoteMessageSelf ?? emoteMessage
                : emoteMessage;

            // resolve localization
            var emote = Loc.GetString(
                emoteTarget,
                ("user", Identity.Entity(args.User, EntityManager)),
                ("target", Identity.Entity(args.Target, EntityManager)));

            // post emote
            _chatSystem.TrySendInGameICMessage(args.User, emote, InGameICChatType.Emote, ChatTransmitRange.Normal);
        }

        // now popup filtered to user - skip if it's self-targeted
        if(!selfTarget)
            _popupSystem.PopupClient(msg, uid, args.User);

        if (proto.SoundPerceivedByOthers)
        {
            _audio.PlayPredicted(sfx, Transform(args.Target).Coordinates, args.User);
        }
        else
        {
            _audio.PlayLocal(sfx, args.Target, args.User);

            // don't play sounds twice if you're the target
            if(args.User != args.Target)
                _audio.PlayEntity(sfx, Filter.Entities(args.Target), args.Target, false);
        }
    }
}
