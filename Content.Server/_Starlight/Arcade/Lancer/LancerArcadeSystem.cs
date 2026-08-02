using Content.Server.Advertise.EntitySystems;
using Content.Server._Starlight.Arcade.Systems;
using Content.Shared.Advertise.Components;
using Content.Shared._Starlight.Arcade.Lancer;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed class LancerArcadeSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SpeakOnUIClosedSystem _speakOnUIClosed = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ArcadeSystem _arcade = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LancerArcadeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<LancerArcadeComponent, AfterActivatableUIOpenEvent>(OnAfterUiOpen);
        SubscribeLocalEvent<LancerArcadeComponent, PowerChangedEvent>(OnPowerChanged);

        Subs.BuiEvents<LancerArcadeComponent>(LancerArcadeUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnAfterUiClose);
            subs.Event<LancerArcadeMessages.LancerPlayerActionMessage>(OnPlayerAction);
        });
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<LancerArcadeComponent>();
        while (query.MoveNext(out _, out var arcade))
        {
            arcade.Game?.Tick(frameTime);
        }
    }

    private void OnComponentInit(EntityUid uid, LancerArcadeComponent component, ComponentInit args)
    {
        component.Game = new LancerGame(
            uid,
            EntityManager,
            _random,
            _prototypes,
            _uiSystem,
            _arcade,
            _audio);
    }

    private void OnAfterUiOpen(EntityUid uid, LancerArcadeComponent component, AfterActivatableUIOpenEvent args)
    {
        if (component.Player == null)
            component.Player = args.User;
        else if (component.Player != args.User && !component.Spectators.Contains(args.User))
            component.Spectators.Add(args.User);

        UpdatePlayerStatus(uid, args.User, component);
        component.Game?.UpdateNewPlayerUi(args.User);
    }

    private void OnAfterUiClose(EntityUid uid, LancerArcadeComponent component, BoundUIClosedEvent args)
    {
        if (component.Player != args.Actor)
        {
            component.Spectators.Remove(args.Actor);
            UpdatePlayerStatus(uid, args.Actor, component);
            return;
        }

        var previous = component.Player;
        if (component.Spectators.Count > 0)
        {
            component.Player = component.Spectators[0];
            component.Spectators.Remove(component.Player.Value);
            UpdatePlayerStatus(uid, component.Player.Value, component);
        }
        else
        {
            component.Player = null;
        }

        if (previous != null)
            UpdatePlayerStatus(uid, previous.Value, component);
    }

    private void OnPowerChanged(EntityUid uid, LancerArcadeComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        // Soft-reset on power loss: return to the opening credit screen (Start Game),
        // without wiping cabinet campaign progress (skills / cleared missions).
        component.Game?.ReturnToIntro();
        _uiSystem.CloseUi(uid, LancerArcadeUiKey.Key);
    }

    private void OnPlayerAction(EntityUid uid, LancerArcadeComponent component, LancerArcadeMessages.LancerPlayerActionMessage msg)
    {
        if (component.Game == null)
            return;

        if (!LancerArcadeUiKey.Key.Equals(msg.UiKey))
            return;

        if (msg.Actor != component.Player)
            return;

        if (TryComp<SpeakOnUIClosedComponent>(uid, out var speakComponent))
            _speakOnUIClosed.TrySetFlag((uid, speakComponent));

        component.Game.ProcessAction(
            msg.Action,
            msg.Cell,
            msg.WeaponIndex,
            msg.TargetUnitId,
            msg.StabilizeOption,
            msg.ContextId);
    }

    private void UpdatePlayerStatus(EntityUid uid, EntityUid actor, LancerArcadeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _uiSystem.ServerSendUiMessage(
            uid,
            LancerArcadeUiKey.Key,
            new LancerArcadeMessages.LancerUserStatusMessage(component.Player == actor),
            actor);
    }
}
