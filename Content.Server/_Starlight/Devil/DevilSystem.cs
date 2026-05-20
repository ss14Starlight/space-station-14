using Content.Shared._Starlight.Devil;
using Content.Server.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Server.RandomMetadata;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Server.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared._Starlight.Sprite;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;
using Content.Shared.Audio;
using Content.Server.Audio;
using Robust.Shared.Audio;
using Content.Shared._Starlight.Paper;
using System.Text.RegularExpressions;
using Content.Shared.Paper;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilComponent, ComponentStartup>(OnStartup, before: [typeof(DamageableSystem)]);
        SubscribeLocalEvent<DevilComponent, SummonDemonicContractEvent>(OnSummonDemonicContract);

        SubscribeLocalEvent<DevilComponent, DevilSoulsDamnedCountChangedEvent>(OnDevilSoulsDamnedCountChanged);

        SubscribeDamned();
        SubscribeBanish();
    }

    private void OnStartup(EntityUid uid, DevilComponent devilComp, ref ComponentStartup args)
    {
        foreach (var action in devilComp.BaseActions) _actions.AddAction(uid, action);

        devilComp.TrueName = _randomMetadata.GetRandomFromSegments(devilComp.NameSegments, devilComp.NameFormat);
    }

    #region abilities
    protected EntityUid CreateContract(EntityUid author, DevilComponent devilComp)
    {
        var paper = Spawn(devilComp.InfernalContractPrototype, Transform(author).Coordinates);
        if (TryComp<InfernalContractComponent>(paper, out var contractComp))
        {
            contractComp.Author = author;
            Dirty<InfernalContractComponent>((paper, contractComp));
        }

        if (TryComp<ParsablePaperComponent>(paper, out var parsableComp))
        {
            // adds true name to the required patterns, as this dynamically changes between devils
            // this is also shit, preferably this would somehow be able to exist entirely in yaml
            var regexSanitisedTruename = NameSanitizeRegex().Replace(devilComp.TrueName, "");
            parsableComp.RequiredPatterns.Add($"(?<={Regex.Escape(regexSanitisedTruename)}, an agent of hell.).*");
        }

        var content = Loc.GetString("infernal-contract-base", ("truename", devilComp.TrueName));
        _paper.SetContent(paper, content);

        _audio.PlayPredicted(devilComp.ContractSummonSound, author, author);

        return paper;
    }

    [GeneratedRegex("^[-, a-zA-Z0-9]")]
    private static partial Regex NameSanitizeRegex();

    private void OnSummonDemonicContract(EntityUid uid, DevilComponent devilComp, ref SummonDemonicContractEvent args)
    {
        var paper = CreateContract(uid, devilComp);
        _hands.TryPickupAnyHand(uid, paper);

        args.Handled = true;
    }
    #endregion

    #region appearance
    private bool FitsChangeCriteria(DevilComponent devil, DevilChangeCriteria criteria) => devil.DamnedSouls.Count >= criteria.AtSouls && !criteria.Completed;

    private void OnDevilSoulsDamnedCountChanged(EntityUid uid, DevilComponent devilComp, ref DevilSoulsDamnedCountChangedEvent args)
    {
        // if chain looks evil but this is the most sensible way I could find to do this
        if (FitsChangeCriteria(devilComp, devilComp.RedEyesAppearance))
        {
            _humanoidAppearance.SetEyeColor(uid, Color.Red);
            _humanoidAppearance.SetMarkingGlowing(uid, MarkingCategories.Eyes, 0, true);
            devilComp.RedEyesAppearance.Completed = true;
        }

        if (FitsChangeCriteria(devilComp, devilComp.EvilHaloAppearance))
        {
            EnsureComp<AppliedSpriteLayerComponent>(uid, out var appliedSpriteLayer);
            appliedSpriteLayer.Sprite = new SpriteSpecifier.Rsi(new ResPath("_Starlight/Devil/evilhalo.rsi"), "halo");
            appliedSpriteLayer.Layer = "devil_halo";
            devilComp.EvilHaloAppearance.Completed = true;
        }

        if (FitsChangeCriteria(devilComp, devilComp.OminousHum))
        {
            EnsureComp<AmbientSoundComponent>(uid);
            _ambientSound.SetSound(uid, new SoundPathSpecifier(new ResPath("/Audio/Weapons/ebladehum.ogg")));
            _ambientSound.SetVolume(uid, -4);
            _ambientSound.SetRange(uid, 10);
            devilComp.OminousHum.Completed = true;
        }

        if (FitsChangeCriteria(devilComp, devilComp.RedAuraAppearance))
        {
            EnsureComp<PointLightComponent>(uid);
            _pointLight.SetColor(uid, Color.Red);
            _pointLight.SetRadius(uid, 2);
            _pointLight.SetEnergy(uid, 3);
            devilComp.RedAuraAppearance.Completed = true;
        }

        if (FitsChangeCriteria(devilComp, devilComp.BidentAction))
        {
            _actions.AddAction(uid, devilComp.SummonBidentActionProto);
            devilComp.BidentAction.Completed = true;
        }

        if (FitsChangeCriteria(devilComp, devilComp.InfernalJauntAction))
        {
            _actions.AddAction(uid, devilComp.InfernalJauntActionProto);
            devilComp.InfernalJauntAction.Completed = true;
        }
    }
    #endregion
}
