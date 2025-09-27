using Content.Shared._Starlight.Devil;
using Content.Shared._Starlight.Paper;
using Robust.Shared.Audio;
using Content.Shared.Paper;
using System.Text.RegularExpressions;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly ParsablePaperSystem _parsablePaper = default!;

    private readonly string InfernalContractPrototype = "InfernalContract";
    private SoundPathSpecifier ContractSummonSound = new("/Audio/Effects/thudswoosh.ogg");

    private void SubscribeContract()
    {

    }

    private EntityUid CreateContract(EntityUid author, DevilComponent devilComp)
    {
        var paper = Spawn(InfernalContractPrototype, Transform(author).Coordinates);
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
            parsableComp.RequiredPatterns.Add($"(?<={regexSanitisedTruename}, an agent of hell.).*");
        }

        var content = Loc.GetString("infernal-contract-base", ("truename", devilComp.TrueName));
        _paper.SetContent(paper, content);

        _audio.PlayPvs(ContractSummonSound, author);

        return paper;
    }

    [GeneratedRegex("^[-, a-zA-Z0-9]")]
    private static partial Regex NameSanitizeRegex();

    #region events
    protected override void OnSignedEvent(EntityUid uid, InfernalContractComponent contractComp, ref PaperSignedEvent args)
    {
        base.OnSignedEvent(uid, contractComp, ref args);
        if (args.Cancelled) return;

        var contract = GetContractContent(uid);
        if (contract == null) return;
        DamnEntity(args.Signer, (InfernalContractData)contract);
    }
    #endregion
}