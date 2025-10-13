using Content.Shared._Starlight.PhysicalSocialInteraction.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.PhysicalSocialInteraction.Systems;

public class PhysicalSocialInteractionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        //subscribe to inspect events on the physical social interaction receiver component
        SubscribeLocalEvent<PhysicalSocialInteractionReceiverComponent, GetVerbsEvent<Verb>>(AddPhysicalSocialInteractionVerbs);
    }

    private void AddPhysicalSocialInteractionVerbs(EntityUid uid, PhysicalSocialInteractionReceiverComponent component, GetVerbsEvent<Verb> args)
    {
        //check if the user also has a interaction giver
        if (!HasComp<PhysicalSocialInteractionGiverComponent>(args.User))
            return;

        //create a verb subcategory
        var category = new VerbCategory("Physical Social Interaction", null);

        //enumerate all the physical social interaction prototypes
        foreach (var protoid in component.InteractionPrototypes)
        {
            //resolve the proto itself
            if (!_protoMan.TryIndex<PhysicalSocialInteractionPrototype>(protoid, out var proto))
                continue;
            
            //make a verb for each one
            Verb verb = new()
            {
                Text = Loc.GetString(proto.VerbName),
                Category = category,
                Act = () =>
                {
                    InteractionPopupAction(uid, args, proto);
                }
            };

            args.Verbs.Add(verb);
        }
    }

    private void InteractionPopupAction(EntityUid uid, GetVerbsEvent<Verb> args, PhysicalSocialInteractionPrototype proto)
    {
        var msg = ""; // Stores the text to be shown in the popup message
        SoundSpecifier? sfx = null; // Stores the filepath of the sound to be played

        if (proto.InteractString != null)
            msg = Loc.GetString(proto.InteractString, ("target", Identity.Entity(args.Target, EntityManager)));

        if (proto.InteractSound != null)
            sfx = proto.InteractSound;

        if (!string.IsNullOrEmpty(proto.MessagePerceivedByOthers))
        {
            var msgOthers = Loc.GetString(proto.MessagePerceivedByOthers,
                ("user", Identity.Entity(args.User, EntityManager)), ("target", Identity.Entity(args.Target, EntityManager)));
            _popupSystem.PopupEntity(msgOthers, uid, Filter.PvsExcept(args.User, entityManager: EntityManager), true);
        }

        //now popup filtered to user
        _popupSystem.PopupClient(msg, uid, args.User);

        if (proto.SoundPerceivedByOthers)
        {
            _audio.PlayPvs(sfx, args.Target);
        }
        else
        {
            _audio.PlayEntity(sfx, Filter.Entities(args.User, args.Target), args.Target, false);
        }
    }
}
