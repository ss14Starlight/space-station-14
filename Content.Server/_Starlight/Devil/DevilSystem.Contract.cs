using Content.Shared._Starlight.Devil;
using Content.Shared._Starlight.Paper;
using Content.Shared.Examine;
using Content.Server.Verbs;
using Robust.Shared.Audio;
using Content.Shared.Paper;
using System.Text.RegularExpressions;
using Content.Shared.Verbs;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly ParsablePaperSystem _parsablePaper = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private readonly string InfernalContractPrototype = "InfernalContract";
    private SoundPathSpecifier ContractSummonSound = new("/Audio/Effects/thudswoosh.ogg");

    private void SubscribeContract()
    {
        SubscribeLocalEvent<InfernalContractComponent, ExaminedEvent>(OnExamineEvent);
    }

    private EntityUid CreateContract(EntityUid author, DevilComponent devilComp)
    {
        var paper = Spawn(InfernalContractPrototype, Transform(author).Coordinates);
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

    private InfernalContractValidity GetContractValidity(EntityUid contract)
    {
        if (!TryComp<InfernalContractComponent>(contract, out var contractComp) || !TryComp<ParsablePaperComponent>(contract, out var parsableComponent))
            return InfernalContractValidity.NotAContract;

        if (!_parsablePaper.IsPaperValid(contract))
            return InfernalContractValidity.InvalidFormat;

        // todo contracts....

        return InfernalContractValidity.Valid;
    }

    [GeneratedRegex("^[-, a-zA-Z0-9]")]
    private static partial Regex NameSanitizeRegex();

    #region events
    private void OnExamineEvent(EntityUid uid, InfernalContractComponent contractComp, ref ExaminedEvent args)
    {
        var contractValidity = GetContractValidity(uid);
        if (contractValidity == InfernalContractValidity.NotAContract) return;

        var stateMessage = Loc.GetString($"infernal-contract-examined-{contractValidity}");
        args.PushMarkup(stateMessage);

        // todo show contract cost
    }
    #endregion
}

public enum InfernalContractValidity
{
    Valid,
    InvalidFormat,
    TooCostly,
    UnknownClauses,
    NotAContract
}