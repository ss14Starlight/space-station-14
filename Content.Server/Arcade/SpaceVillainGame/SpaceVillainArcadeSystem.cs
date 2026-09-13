using Content.Server.Power.Components;
using Content.Shared.UserInterface;
using Content.Server.Advertise.EntitySystems;
using Content.Shared.Advertise.Components;
using Content.Shared.Power;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server._Starlight.Arcade.Systems;
using Content.Shared.Arcade.SpaceVillain;
using Content.Shared._Starlight.Arcade.Systems;

namespace Content.Server.Arcade.SpaceVillain;

public sealed partial class SpaceVillainArcadeSystem : SharedSpaceVillainArcadeSystem // Starlight-edit
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private SpeakOnUIClosedSystem _speakOnUIClosed = default!;
    [Dependency] private ArcadeSystem _arcade = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpaceVillainArcadeComponent, AfterActivatableUIOpenEvent>(OnAfterUIOpenSV);
        SubscribeLocalEvent<SpaceVillainArcadeComponent, SpaceVillainArcadePlayerActionMessage>(OnSVPlayerAction); // Starlight-edit
        SubscribeLocalEvent<SpaceVillainArcadeComponent, PowerChangedEvent>(OnSVillainPower);
    }

    /// <summary>
    /// Picks a fight-verb from the list of possible Verbs.
    /// </summary>
    /// <returns>A fight-verb.</returns>
    public string GenerateFightVerb(SpaceVillainArcadeComponent arcade)
    {
        return _random.Pick(_prototypeManager.Index(arcade.PossibleFightVerbs));
    }

    /// <summary>
    /// Generates an enemy-name comprised of a first- and last-name.
    /// </summary>
    /// <returns>An enemy-name.</returns>
    public string GenerateEnemyName(SpaceVillainArcadeComponent arcade)
    {
        var possibleFirstEnemyNames = _prototypeManager.Index(arcade.PossibleFirstEnemyNames);
        var possibleLastEnemyNames = _prototypeManager.Index(arcade.PossibleLastEnemyNames);

        return $"{_random.Pick(possibleFirstEnemyNames)} {_random.Pick(possibleLastEnemyNames)}";
    }

    private void OnSVPlayerAction(EntityUid uid, SpaceVillainArcadeComponent component, SpaceVillainArcadePlayerActionMessage msg)
    {
        if (component.Game == null)
            return;
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var power) || !power.Powered)
            return;

        switch (msg.PlayerAction)
        {
            // Starlight-start
            case SpaceVillainPlayerAction.Attack:
            case SpaceVillainPlayerAction.Heal:
            case SpaceVillainPlayerAction.Recharge:
                component.Game.ExecutePlayerAction(uid, msg.PlayerAction, component);
                // Any sort of gameplay action counts
                if (TryComp<SpeakOnUIClosedComponent>(uid, out var speakComponent))
                    _speakOnUIClosed.TrySetFlag((uid, speakComponent));
                break;
            case SpaceVillainPlayerAction.NewGame:
                _audioSystem.PlayPvs(component.NewGameSound, uid, AudioParams.Default.WithVolume(SharedArcadeSystem.ArcadeSoundVolumeDb));

                component.Game = new SpaceVillainGame(component, this, _arcade);
                _uiSystem.ServerSendUiMessage(uid, SpaceVillainArcadeUiKey.Key, component.Game.GenerateMetaDataMessage());
                break;
            case SpaceVillainPlayerAction.RequestData:
                _uiSystem.ServerSendUiMessage(uid, SpaceVillainArcadeUiKey.Key, component.Game.GenerateMetaDataMessage());
                break;
            // Starlight-end
        }
    }

    private void OnAfterUIOpenSV(EntityUid uid, SpaceVillainArcadeComponent component, AfterActivatableUIOpenEvent args)
    {
        component.Game ??= new(component, this, _arcade); // Starlight-edit
    }

    private void OnSVillainPower(EntityUid uid, SpaceVillainArcadeComponent component, ref PowerChangedEvent args)
    {
        if (TryComp<ApcPowerReceiverComponent>(uid, out var power) && power.Powered)
            return;

        _uiSystem.CloseUi(uid, SpaceVillainArcadeUiKey.Key); // Starlight-edit
    }
}
