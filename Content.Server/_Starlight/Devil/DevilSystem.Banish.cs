using Content.Server.Damage.Systems;
using Content.Shared._Starlight.Devil;
using Content.Server._Starlight.Bible;
using Content.Shared.Dataset;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Server.Jittering;
using Content.Server.Chat.Systems;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly JitteringSystem _jittering = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

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
        bool containsBanish = false;
        foreach (var banish in BanishPhrases)
        {
            if(!message.Contains(banish, StringComparison.InvariantCultureIgnoreCase)) continue;
            containsBanish = true;
            break;
        }

        return containsBanish;
    }

    private void OnListen(EntityUid uid, DevilComponent devilComp, ref ListenEvent args)
    {
        // here we check if we are going to banish with this message
        if(!devilComp.BeingBanished) return;
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
        if (devilComp.BeingBanished) return;

        devilComp.BeingBanished = true;
        devilComp.LastBanishModeActivate = _time.CurTime;
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("devil-banish-initiate", ("devil", Name(uid))), uid, PopupType.MediumCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DevilComponent>();
        while (query.MoveNext(out var uid, out var devilComp))
        {
            // handle turning off banishment
            if (devilComp.BeingBanished && (devilComp.LastBanishModeActivate + devilComp.BanishModeLength) < _time.CurTime)
            {
                devilComp.BeingBanished = false;
            }

            if (devilComp.BeingBanished)
            {
                _jittering.DoJitter(uid, TimeSpan.FromSeconds(3), true, amplitude: 5, frequency: 12);

                bool random = _random.Prob(0.33f);
                if (random)
                {
                    string emote = _random.Prob(0.5f) ? "screams" : "spasms";
                    _chat.TryEmoteWithChat(uid, emote, forceEmote: true);
                }
            }
        }
    }
}