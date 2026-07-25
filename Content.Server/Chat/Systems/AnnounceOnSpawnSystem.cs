using Content.Server.Chat;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

public sealed partial class AnnounceOnSpawnSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnnounceOnSpawnComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(EntityUid uid, AnnounceOnSpawnComponent comp, MapInitEvent args)
    {
        // Starlight begin
        var mapUid = Transform(uid).MapUid;
        if (mapUid is not null && MetaData(mapUid.Value).EntityName.StartsWith("ATAM-") && comp.IgnoreASpace) return;
        // Starlight end
        var message = Loc.GetString(comp.Message);
        var sender = comp.Sender != null ? Loc.GetString(comp.Sender) : Loc.GetString("chat-manager-sender-announcement");
        // Starlight begin
        if (comp.GlobalAnnounce) _chat.DispatchGlobalAnnouncement(message, sender, playSound: true, comp.Sound, comp.Color);
        else
        {
            var playersOnMap = Filter.Empty().AddInMap(Transform(uid).MapID);
            _chat.DispatchFilteredAnnouncement(playersOnMap, message, null, sender, playSound: true, comp.Sound,
                comp.Color);
        }
        // Starlight end
    }
}
