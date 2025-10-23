using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._Starlight.Paper;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil;

public abstract partial class SharedDevilSystem : EntitySystem
{
    [Dependency] private readonly ParsablePaperSystem _parsablePaper = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfernalContractComponent, ExaminedEvent>(OnExamineEvent);
        SubscribeLocalEvent<InfernalContractComponent, PaperSignedEvent>(OnSignedEvent);
    }

    #region contract
    protected InfernalContractValidity GetContractValidity(EntityUid contract)
    {
        if (!TryComp<InfernalContractComponent>(contract, out var contractComp) || !TryComp<ParsablePaperComponent>(contract, out var parsableComponent))
            return InfernalContractValidity.NotAContract;

        if (!_parsablePaper.IsPaperValid(contract))
            return InfernalContractValidity.InvalidFormat;

        if (contractComp.Completed)
            return InfernalContractValidity.Signed;

        return InfernalContractValidity.Valid;
    }

    protected InfernalContractData? GetContractContent(EntityUid contract)
    {
        if (!TryComp<InfernalContractComponent>(contract, out var contractComp) || !TryComp<ParsablePaperComponent>(contract, out var parsableComponent))
            return null;

        InfernalContractData data;
        data.Damnations = new();
        data.Cost = 0;

        // welcome to serialization hell
        // one regex statement can only take us so far, we need a second to break them down into individual lines
        var rawContent = _parsablePaper.GetPaperValues(contract, true);
        if (rawContent == null) return null;

        var rawSacrificesGroup = rawContent.GetValueOrDefault("sacrifices")![0];
        var rawBenefitsGroup = rawContent.GetValueOrDefault("benefits")![0];

        var listSplitterRegex = new Regex("[•\\-\\.\\+]\\s*(.+)"); // bruh

        var rawSacrifices = listSplitterRegex.Matches(rawSacrificesGroup).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        var rawBenefits = listSplitterRegex.Matches(rawBenefitsGroup).Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        // todo refactor this craziness, make sacrifices/benefits not seperate?
        var rawDamnations = rawSacrifices.Concat(rawBenefits);

        // we now have our string arrays of the wanted effects. Now we need to check them against existing ones.
        // todo check for duplicates
        if (!TryComp<DevilComponent>(contractComp.Author, out var devilComp)) return null;
        var availableDamnations = devilComp.AvailableDamnations.Select(d =>
        {
            _prototype.TryIndex<DamnationPrototype>(d, out var damnationProto);
            return damnationProto!.Name.ToLower();
        }).ToList();
        foreach (var damnation in rawDamnations)
        {
            var index = availableDamnations.IndexOf(damnation.ToLower());
            if (index != -1)
            {
                data.Damnations.Add(devilComp.AvailableDamnations[index]);
            }
        }
        data.Damnations = data.Damnations.Distinct().ToList();

        foreach (var damnation in data.Damnations)
        {
            if (_prototype.TryIndex<DamnationPrototype>(damnation, out var damnationProto))
                data.Cost += damnationProto.Cost;
        }

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
    }

    protected virtual void OnSignedEvent(EntityUid uid, InfernalContractComponent contractComp, ref PaperSignedEvent args)
    {
        if (args.Cancelled || contractComp.Completed) return;

        if (GetContractValidity(uid) != InfernalContractValidity.Valid)
        {
            args.FailReason = Loc.GetString("infernal-contract-popup-fail");
            args.Cancelled = true;
            return;
        }

        if (HasComp<DevilComponent>(args.Signer) || HasComp<DamnedComponent>(args.Signer))
        {
            args.FailReason = Loc.GetString("infernal-contract-popup-fail-self");
            args.Cancelled = true;
            return;
        }
    }
    #endregion
}

public enum InfernalContractValidity
{
    Valid,
    InvalidFormat,
    TooCostly,
    NotAContract,
    Signed
}

/// <summary>
/// 
/// </summary>
public record struct InfernalContractData
{
    public int Cost;

    public List<ProtoId<DamnationPrototype>> Damnations;
}