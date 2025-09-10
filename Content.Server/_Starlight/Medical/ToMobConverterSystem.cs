using Content.Shared.DoAfter;
using Content.Shared.Verbs;
using Content.Shared._Starlight.Medical;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Server.Body.Systems;
using Content.Server.Humanoid.Systems;
using Content.Shared.Humanoid;
using Content.Server.Humanoid;
using Content.Shared.Preferences;
using Content.Server.NPC.Queries.Considerations;

namespace Content.Server._Starlight.Medical;

public sealed class ToMobConverterSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly ISawmill _sawmill = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToMobConverterComponent, ConvertToMobDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<ConvertableToMobComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
    }

    private void OnDoAfter(EntityUid uid, ToMobConverterComponent mobConvComp, DoAfterEvent args)
    {
        if (args.Handled || !TryComp<ConvertableToMobComponent>(args.Args.Target, out var convToMobComp)) return;
        mobConvComp.Converting.Remove(args.Args.Target.Value); // done, cancelled or not
        if (args.Cancelled) return;
        QueueDel(args.Target); // delete the torso

        var coords = _transform.GetMapCoordinates(args.Args.Target.Value);
        var ent = Spawn(convToMobComp.OutputMob, coords);

        if (!TryComp<BodyComponent>(ent, out var _))
        {
            QueueDel(ent);
            _sawmill.Error($"When producing mob from torso, was asked to create {convToMobComp.OutputMob} which has no BodyComponent");
        }

        foreach (var organ in _body.GetBodyOrgans(ent)) QueueDel(organ.Id); // clear out organs
        foreach (var part in _body.GetBodyChildren(ent)) // clear out non-torso parts
        {
            if (part.Component.PartType == BodyPartType.Torso) continue;
            QueueDel(part.Id);
        }

        // randomise appearance if possible
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid)) return;

        var newProfile = HumanoidCharacterProfile.RandomWithSpecies(humanoid.Species);
        _humanoidAppearance.LoadProfile(ent, newProfile, humanoid);
        _metaData.SetEntityName(ent, newProfile.Name, raiseEvents: false);
   }

    private void OnGetVerbs(EntityUid uid, ConvertableToMobComponent comp, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract) return;

        InteractionVerb verb = new()
        {
            Text = Loc.GetString("tomobconvert-verb-name"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => TryStartConvert(args.Using!.Value, args.Target, args.User)
        };
        args.Verbs.Add(verb);
    }

    private bool TryStartConvert(EntityUid tool, EntityUid torso, EntityUid user)
    {
        if (!TryComp<ToMobConverterComponent>(tool, out var mobConvComp)) return false;
        if (!TryComp<ConvertableToMobComponent>(torso, out var convToMobComp)) return false;

        if (!mobConvComp.Converting.Add(torso)) return false;

        var doAft = new DoAfterArgs(EntityManager, user, convToMobComp.ConvertDelay, new ConvertToMobDoAfterEvent(), tool, target: torso, used: tool)
        {
            BreakOnMove = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAft);

        return true;
    }
}