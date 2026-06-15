using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.Popups; // Starlight
using Content.Shared.RatKing.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing; // Starlight

namespace Content.Shared.RatKing.Systems;

public sealed class RummagerSystem : EntitySystem
{
    [Dependency] private readonly EntityTableSystem _entityTable =  default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // Starlight
    [Dependency] private readonly SharedPopupSystem _popup = default!; // Starlight

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RummageableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerb);
        SubscribeLocalEvent<RummageableComponent, RummageDoAfterEvent>(OnDoAfterComplete);
    }

    private void OnGetVerb(Entity<RummageableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        Log.Warning("Rummager OnGetVerb hit");
        // Starlight start
        if (ent.Comp.Looted && _timing.CurTime >= ent.Comp.NextRummageTime)
        {
            ent.Comp.Looted = false;
            Dirty(ent, ent.Comp);
            Log.Warning("Container looted expiry met, marking as unlooted.");
        }
        // Starlight end

        if (!HasComp<RummagerComponent>(args.User))
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rat-king-rummage-text"),
            Priority = 0,
            Act = () =>
            {
                if (ent.Comp.Looted)
                {
                    Log.Warning("MESSAGE HERE THAT IS ALREADY LOOTED");
                    _popup.PopupCursor(Loc.GetString("rummage-already-looted"), user, PopupType.SmallCaution);
                    return;
                }

                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
                    user,
                    ent.Comp.RummageDuration,
                    new RummageDoAfterEvent(),
                    ent,
                    ent)
                {
                    BlockDuplicate = true,
                    BreakOnDamage = true,
                    BreakOnMove = true,
                    DistanceThreshold = 2f
                });
            }
        });
    }

    private void OnDoAfterComplete(Entity<RummageableComponent> ent, ref RummageDoAfterEvent args)
    {
        Log.Warning("Rummager OnDoAfterComplete hit");
        if (args.Cancelled || ent.Comp.Looted)
        {
            Log.Warning("DoAfterComplete, aborting");
            return;
        }

        // // Starlight begin
        // if (args.Cancelled)
        //     return;
        //
        // if (ent.Comp.Looted)
        // {
        //     _popup.PopupCursor(Loc.GetString("rummage-already-looted"), args.User, PopupType.SmallCaution);
        //     return;
        // }
        // // Starlight end

        Log.Warning("DoAfterComplete, continuing.");
        ent.Comp.Looted = true;
        ent.Comp.NextRummageTime = _timing.CurTime + ent.Comp.LootResetDelay; // Starlight
        Dirty(ent, ent.Comp);
        _audio.PlayPredicted(ent.Comp.Sound, ent, args.User);

        var spawns = _entityTable.GetSpawns(ent.Comp.Table);
        var coordinates = Transform(ent).Coordinates;

        foreach (var spawn in spawns)
        {
            Spawn(spawn, coordinates);
        }
    }
}

/// <summary>
/// DoAfter event for rummaging through a container with RummageableComponent.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RummageDoAfterEvent : SimpleDoAfterEvent;
