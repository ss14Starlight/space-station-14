using Content.Server.Chat.Systems;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Popups;
using Content.Shared.Chat;

namespace Content.Server._Starlight.CosmicCult.EntitySystems;
public sealed partial class DeconversionJailSystem : SharedDeconversionJailSystem
{
    [Dependency] private ChatSystem _chat = default!;

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
