using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._Starlight.Paper;
using Content.Shared.Examine;
using Content.Shared.Paper;

namespace Content.Shared._Starlight.Devil;

public abstract partial class SharedDevilSystem : EntitySystem
{
    [Dependency] private readonly ParsablePaperSystem _parsablePaper = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfernalContractComponent, ExaminedEvent>(OnExamineEvent);
        SubscribeLocalEvent<InfernalContractComponent, PaperSignedEvent>(OnSignedEvent);
    }

    protected InfernalContractValidity GetContractValidity(EntityUid contract)
    {
        if (!TryComp<InfernalContractComponent>(contract, out var contractComp) || !TryComp<ParsablePaperComponent>(contract, out var parsableComponent))
            return InfernalContractValidity.NotAContract;

        if (!_parsablePaper.IsPaperValid(contract))
            return InfernalContractValidity.InvalidFormat;

        if (contractComp.Completed)
            return InfernalContractValidity.Signed;

        // todo contracts....

        return InfernalContractValidity.Valid;
    }

    protected InfernalContractData? GetContractContent(EntityUid contract)
    {
        if (!TryComp<InfernalContractComponent>(contract, out var contractComp) || !TryComp<ParsablePaperComponent>(contract, out var parsableComponent))
            return null;

        InfernalContractData data;

        // welcome to serialization hell
        // one regex statement can only take us so far, we need a second to break them down into individual lines
        var rawContent = _parsablePaper.GetPaperValues(contract, true);
        if (rawContent == null) return null;

        var rawSacrificesGroup = rawContent.GetValueOrDefault("sacrifices")![0];
        var rawBenefitsGroup = rawContent.GetValueOrDefault("benefits")![0];

        var listSplitterRegex = new Regex("[•\\-\\.\\+]\\s*(.+)");

        var rawSacrifices = listSplitterRegex.Matches(rawSacrificesGroup).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        var rawBenefits = listSplitterRegex.Matches(rawBenefitsGroup).Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        // we now have our string arrays of the wanted effects. Now we need to check them against existing ones.

        data.Cost = 0;

        return data;
    }

    private void OnExamineEvent(EntityUid uid, InfernalContractComponent contractComp, ref ExaminedEvent args)
    {
        var contractValidity = GetContractValidity(uid);
        if (contractValidity == InfernalContractValidity.NotAContract) return;

        args.PushMarkup(Loc.GetString($"infernal-contract-examined-{contractValidity}"));

        var contractData = GetContractContent(uid);
        if (contractData != null)
            args.PushMarkup(Loc.GetString("infernal-contract-examine-cost", ("value", contractData.Value.Cost)));

        // todo show contract cost
    }

    private void OnSignedEvent(EntityUid uid, InfernalContractComponent contractComp, ref PaperSignedEvent args)
    {
        if (args.Cancelled || contractComp.Completed) return;

        if (GetContractValidity(uid) != InfernalContractValidity.Valid || args.Signer == contractComp.Author)
        {
            args.FailReason = args.Signer == contractComp.Author ?
                Loc.GetString("infernal-contract-popup-fail-self") :
                Loc.GetString("infernal-contract-popup-fail");
            args.Cancelled = true;

            return;
        }
    }
}

public enum InfernalContractValidity
{
    Valid,
    InvalidFormat,
    TooCostly,
    UnknownClauses,
    NotAContract,
    Signed
}

/// <summary>
/// 
/// </summary>
public record struct InfernalContractData
{
    public int Cost;

    // todo sacrifices/benefits
}