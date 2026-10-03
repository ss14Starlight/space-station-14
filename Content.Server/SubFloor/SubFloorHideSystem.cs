using Content.Shared.Eye;
using Content.Shared.SubFloor;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.SubFloor;

public sealed partial class SubFloorHideSystem : SharedSubFloorHideSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    private Dictionary<ICommonSession, int> _showFloors = new(); //Starlight edit - Subfloor layers

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ShowSubfloorRequestEvent>(OnShowSubfloor);
        SubscribeLocalEvent<GetVisMaskEvent>(OnGetVisibility);

        _player.PlayerStatusChanged += OnPlayerStatus;
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected)
            return;

        _showFloors.Remove(e.Session);

        if (e.Session.AttachedEntity != null)
            _eye.RefreshVisibilityMask(e.Session.AttachedEntity.Value);
    }

    private void OnGetVisibility(ref GetVisMaskEvent ev)
    {
        if (!TryComp(ev.Entity, out ActorComponent? actor))
            return;

        if (_showFloors.ContainsKey(actor.PlayerSession))
        {
            ev.VisibilityMask |= (int)VisibilityFlags.Subfloor;
        }
    }

    private void OnShowSubfloor(ShowSubfloorRequestEvent ev, EntitySessionEventArgs args)
    {
        // TODO: Commands are a bit of an eh? for client-only but checking shared perms
        var ent = args.SenderSession.AttachedEntity;

        if (!TryComp(ent, out EyeComponent? eyeComp))
            return;

        //Starlight start - Subfloor layers
        if (ev.Value)
        {
            if (_showFloors.ContainsKey(args.SenderSession))
            {
                _showFloors[args.SenderSession] |= ev.Layer;
            }
            else
            {
                _showFloors[args.SenderSession] = ev.Layer;
            }
        }
        else
        {
            if (_showFloors.ContainsKey(args.SenderSession))
            {
                _showFloors[args.SenderSession] &= ~ev.Layer;
                if (!ev.Value && ev.Layer == 0)
                {
                    _showFloors.Remove(args.SenderSession);
                }
            }
        }
        //Starlight end - Subfloor layers

        _eye.RefreshVisibilityMask((ent.Value, eyeComp));

        RaiseNetworkEvent(new ShowSubfloorRequestEvent()
        {
            Value = ev.Value,
        }, args.SenderSession);
    }
}
