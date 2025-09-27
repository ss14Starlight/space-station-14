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

    private void OnExamineEvent(EntityUid uid, InfernalContractComponent contractComp, ref ExaminedEvent args)
    {
        var contractValidity = GetContractValidity(uid);
        if (contractValidity == InfernalContractValidity.NotAContract) return;

        var stateMessage = Loc.GetString($"infernal-contract-examined-{contractValidity}");
        args.PushMarkup(stateMessage);

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