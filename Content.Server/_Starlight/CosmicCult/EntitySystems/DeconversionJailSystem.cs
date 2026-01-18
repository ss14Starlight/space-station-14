using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Chat;

namespace Content.Server._Starlight.CosmicCult;
public sealed partial class DeconversionJailSystem : SharedDeconversionJailSystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeconversionOublietteComponent>();

        while (query.MoveNext(out _, out var comp))
        {
            if (comp.OublietteState == OublietteStates.Active && Timing.CurTime > comp.EmoteTime && comp.Victim is not null)
            {
                comp.EmoteTime = Timing.CurTime + Random.Next(comp.EmoteMinTime, comp.EmoteMaxTime);
                _chat.TryEmoteWithChat(comp.Victim.Value, "Scream", ChatTransmitRange.Normal, false, null, true, true);
                PopUp.PopupEntity(Loc.GetString("cosmic-oubliette-random-horror", ("COUNT", Random.Next(1, 7))), comp.Victim.Value, comp.Victim.Value, PopupType.MediumCaution);
            }
        }
    }
}
