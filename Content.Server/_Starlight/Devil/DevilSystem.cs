using Content.Shared._Starlight.Devil;
using Content.Server.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Server.RandomMetadata;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Speech.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Vampire.Components;
using Content.Server.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared._Starlight.Sprite;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.Manager;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilComponent, ComponentStartup>(OnStartup, before: [typeof(DamageableSystem)]);

        SubscribeLocalEvent<DevilComponent, SummonDemonicContractEvent>(OnSummonDemonicContract);
        SubscribeLocalEvent<DevilComponent, OpenDamnationsMenuEvent>(OnOpenDamnationsMenu);

        SubscribeLocalEvent<DevilComponent, DevilSoulsDamnedCountChangedEvent>(OnDevilSoulsDamnedCountChanged);

        SubscribeContract();
        SubscribeDamned();
        SubscribeBanish();
    }

    private void OnStartup(EntityUid uid, DevilComponent devilComp, ref ComponentStartup args)
    {
        foreach (var action in devilComp.BaseActions) _actions.AddAction(uid, action);

        devilComp.TrueName = _randomMetadata.GetRandomFromSegments(devilComp.NameSegments, devilComp.NameFormat);
    }

    #region abilities
    private void OnSummonDemonicContract(EntityUid uid, DevilComponent devilComp, ref SummonDemonicContractEvent args)
    {
        var paper = CreateContract(uid, devilComp);
        _hands.TryPickupAnyHand(uid, paper);

        args.Handled = true;
    }

    private void OnOpenDamnationsMenu(EntityUid uid, DevilComponent devilComp, ref OpenDamnationsMenuEvent args)
    {
        if (!TryComp<UserInterfaceComponent>(uid, out var userInterfaceComp) || !TryComp<ActorComponent>(uid, out var actorComp)) return;

        var uiState = new DevilDamnationsBuiState(devilComp.AvailableDamnations);
        _userInterface.SetUiState((uid, userInterfaceComp), DamnationsMenuUiKey.Key, uiState);
        _userInterface.TryToggleUi((uid, userInterfaceComp), DamnationsMenuUiKey.Key, actorComp.PlayerSession);
    }
    #endregion

    #region appearance
    private void OnDevilSoulsDamnedCountChanged(EntityUid uid, DevilComponent devilComp, ref DevilSoulsDamnedCountChangedEvent args)
    {
        if(devilComp.DamnedSouls.Count >= devilComp.RedEyesAppearance.AtSouls && !devilComp.RedEyesAppearance.Completed)
        {
            _humanoidAppearance.SetEyeColor(uid, Color.Red);
            _humanoidAppearance.SetMarkingGlowing(uid, MarkingCategories.Eyes, 0, true);
            devilComp.RedEyesAppearance.Completed = true;
        }

        if(devilComp.DamnedSouls.Count >= devilComp.EvilHaloAppearance.AtSouls && !devilComp.EvilHaloAppearance.Completed)
        {
            AppliedSpriteLayerComponent appliedSpriteLayer = new()
            {
                Sprite = new SpriteSpecifier.Rsi(new ResPath("_Starlight/Devil/evilhalo.rsi"), "halo"),
                Layer = "devil_halo"
            };
            EntityManager.AddComponent(uid, appliedSpriteLayer, true);
        }
    }
    #endregion
}