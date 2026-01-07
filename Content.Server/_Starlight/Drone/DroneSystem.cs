using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Server.Tools.Innate;
using Content.Shared.Body.Components;
using Content.Shared.Drone;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Timing;
using Robust.Server.GameObjects;
using Content.Shared.Mind.Components;
using Content.Shared.Access.Systems;

namespace Content.Server.Drone;

public sealed class DroneSystem : SharedDroneSystem
{
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly InnateToolSystem _innateToolSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DroneComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DroneComponent, ExaminedEvent>(OnExamined);
        // SubscribeLocalEvent<DroneComponent, EmoteAttemptEvent>(OnEmoteAttempt);
        // SubscribeLocalEvent<DroneComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<DroneComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<DroneComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnExamined(EntityUid uid, DroneComponent component, ExaminedEvent args)
        => args.PushMarkup(Loc.GetString("drone-active"));

    private void OnMobStateChanged(EntityUid uid, DroneComponent drone, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            if (TryComp<InnateToolComponent>(uid, out var innate))
                _innateToolSystem.Cleanup(uid, innate);

            if (TryComp<BodyComponent>(uid, out var body))
                _bodySystem.GibBody(uid, body: body);
            QueueDel(uid);
        }
    }

    // private void OnEmoteAttempt(EntityUid uid, DroneComponent component, EmoteAttemptEvent args)
    // {
    //     // Allow screaming with borg sounds, block other emotes
    //     if (args.Emote.ID != "Scream")
    //         args.Cancel();
    // }

    // private void OnThrowAttempt(EntityUid uid, DroneComponent drone, ThrowAttemptEvent args)
    //     => args.Cancel();

    private void OnMindAdded(EntityUid uid, DroneComponent component, MindAddedMessage args)
    {
        UpdateDroneAppearance(uid, DroneStatus.On);
        _access.SetAccessEnabled(uid, true);
    }

    private void OnMindRemoved(EntityUid uid, DroneComponent component, MindRemovedMessage args)
    {
        UpdateDroneAppearance(uid, DroneStatus.Off);
        _access.SetAccessEnabled(uid, false);
    }

    private void UpdateDroneAppearance(EntityUid uid, DroneStatus status)
        => _appearanceSystem.SetData(uid, DroneVisuals.Status, status);
}
