using Content.Server.Damage.Systems;
using Content.Shared._Starlight.Devil;
using Content.Server._Starlight.Bible;
using Content.Shared.Dataset;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using Content.Server.Bible.Components;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private IGameTiming _time = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private ProtoId<LocalizedDatasetPrototype> BanishPhraseDataset = "DevilBanishPhrases";
    private List<string> BanishPhrases = new();

    private void SubscribeBanish()
    {
        SubscribeLocalEvent<DevilComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<DevilComponent, BibleThwackEvent>(OnBibleThwack);

        FillBanishPhrases();
    }

    private void FillBanishPhrases()
    {
        // fill our list with values from dataset
        var banishPhraseProto = _proto.Index(BanishPhraseDataset)!;
        for (int i = 1; i <= banishPhraseProto.Values.Count; i++)
            BanishPhrases.Add(Loc.GetString($"{banishPhraseProto.Values.Prefix}{i}"));
    }

    private bool MessageContainsBanish(string message)
    {
        foreach (var banish in BanishPhrases)
        {
            if (message.Contains(banish, StringComparison.InvariantCultureIgnoreCase)) return true;
        }

        return false;
    }

    private void OnListen(EntityUid uid, DevilComponent devilComp, ref ListenEvent args)
    {
        if(HasComp<DevilComponent>(args.Source) || HasComp<DamnedComponent>(args.Source)) return;
        if(!args.OriginalMessage.Contains(devilComp.TrueName, StringComparison.InvariantCultureIgnoreCase)) return;
        if(!MessageContainsBanish(args.OriginalMessage)) return;

        // ok so we are trying to stop them, can we?
        if(devilComp.LastBanishedList.TryGetValue(args.Source, out var last) && (last + devilComp.BanishCooldown) > _time.CurTime) return;

        // we can
        // todo trigger funny emote of some variety, horns of babylon or something
        _damageable.TryChangeDamage(uid, devilComp.BanishDamage, true);
        _stamina.TakeStaminaDamage(uid, devilComp.BanishDamageStamina);

        devilComp.LastBanishedList[args.Source] = _time.CurTime;
    }

    private void OnBibleThwack(EntityUid uid, DevilComponent devilComp, ref BibleThwackEvent args)
    {
        if (!_mobState.IsDead(uid)) return; // devil not dead
        if (!HasComp<BibleUserComponent>(args.User)) return; // not a chaplain

        // hit while crit/dead, this should super kill them
        _damageable.TryChangeDamage(uid, devilComp.BibleBanishDamage, true);
        var unrevivable = AddComp<UnrevivableComponent>(uid);
        unrevivable.ReasonMessage = "defibrillator-damned";

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("devil-banish-initiate", ("devil", Name(uid))), uid, PopupType.MediumCaution);
    }
}
