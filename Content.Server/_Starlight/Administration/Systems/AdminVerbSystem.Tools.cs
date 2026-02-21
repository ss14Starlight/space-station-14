using Content.Server._Starlight.Antags;
using Content.Server._Starlight.UXN;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Shared._Starlight.UXN;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using static Content.Server.Administration.Systems.AdminVerbSystem;

namespace Content.Server.Starlight.Administration.Systems;
public sealed partial class AdminVerbSystem : EntitySystem
{
    [Dependency] private readonly AdminTestArenaSystem _adminTestArenaSystem = default!;
    [Dependency] private readonly ISharedAdminManager _adminManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IResourceManager _resourceManager= default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddVerbs);
    }
    private void AddVerbs(GetVerbsEvent<Verb> args)
    {
        if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Admin))
            return;

        if (_adminManager.HasAdminFlag(player, AdminFlags.Admin))
        {
            Verb sendToTestArena = new()
            {
                Text = "Reset test arena",
                Category = VerbCategory.Tricks,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),

                Act = () =>
                {
                    //we technically load the map here, but it doesnt matter, this is safer since the behaviour of this function is garunteed
                    //and reimplementing it would be stupid and unsafe
                    var data = _adminTestArenaSystem.AssertArenaLoaded(player);

                    var _mapManager = _entities.System<SharedMapSystem>();

                    //we need to get the actual map ID, so first get the transform
                    if (!_entities.TryGetComponent(data.Map, out TransformComponent? transform))
                        return;
                    
                    //then get the map ID from the transform
                    MapId mapId = transform.MapID;

                    //call remove map on it
                    _mapManager.DeleteMap(mapId);
                    //_transformSystem.SetCoordinates(args.Target, new EntityCoordinates(data.gridUid ?? data.mapUid, Vector2.One));
                },
                Impact = LogImpact.Medium,
                Message = Loc.GetString("admin-trick-reset-test-arena-description"),
                Priority = (int)TricksVerbPriorities.SendToTestArena,
            };
            args.Verbs.Add(sendToTestArena);

            Verb preventObjectiveTargeting = new()
            {
                Text = "Prevent objective targeting",
                Category = VerbCategory.Tricks,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/sentient.svg.192dpi.png")),
                Act = () =>
                {
                    EnsureComp<NoObjectiveTargetComponent>(args.Target);
                    _chat.SendAdminAnnouncementMessage(player, $"Added NoObjectiveTarget component to the entity! ({args.Target})");
                },
                Impact = LogImpact.Low,
                Message = "Prevents this entity from being targeted by other player's objectives. Will also prevent paraclones of this player.",
                Priority = (int)TricksVerbPriorities.BlockObjectiveTargeting
            };
            if (HasComp<ActorComponent>(args.Target)) args.Verbs.Add(preventObjectiveTargeting);
        }

        #region Starlight
        if (_adminManager.HasAdminFlag(player, AdminFlags.Debug))
        {
            // TODO: make these in-game tools in some-way (cause hexdumps are REALLY usefull)
            if (TryComp<UxnComponent>(args.Target, out var uxn))
                args.Verbs.Add(new()
                {
                    Act = () => {
                        var writer = _resourceManager.UserData.OpenWrite(new ResPath("/uxn-dump.bin"));
                        writer.Write([.. uxn.CompiledRom]);
                        writer.Close();
                    },
                    Text = "Dump ROM",
                    Message = "Dumps the rom of the UXN chip to /uxn-dump.bin"
                });
            if (TryComp<UxnAttachedComponent>(args.Target, out var attached))
                args.Verbs.Add(new()
                {
                    Act = () => {
                        var uxn = attached.Uxn!;
                        var writer = _resourceManager.UserData.OpenWrite(new ResPath("/uxn-running-rom.bin"));
                        writer.Write([.. uxn.SystemMem._inner]);
                        writer.Close();
                        writer = _resourceManager.UserData.OpenWrite(new ResPath("/uxn-runniing-device.bin"));
                        writer.Write([.. uxn.DevMem._inner]);
                        writer.Close();               
                    },
                    Text = "Dump UXN",
                    Message = "Dumps the ram/device memory/working/return stacks to various uxn-running-*.bin files"
                });
        }
        #endregion
    }
}
