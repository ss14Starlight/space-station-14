using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._Starlight.Paper;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil;

public abstract partial class SharedDevilSystem : EntitySystem
{
    [Dependency] private readonly ParsablePaperSystem _parsablePaper = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfernalContractComponent, ExaminedEvent>(OnExamineEvent);
        SubscribeLocalEvent<InfernalContractComponent, PaperSignedEvent>(OnSignedEvent);
        SubscribeLocalEvent<InfernalContractComponent, PaperWriteAttemptEvent>(OnPaperWriteAttempt);

        SubscribeLocalEvent<DevilComponent, OpenDamnationsMenuEvent>(OnOpenDamnationsMenu);

        SubscribeLocalEvent<DamnedComponent, DamnationInitFailEvent>(OnDamnationInitFail);
        SubscribeLocalEvent<DamnedComponent, ComponentShutdown>(OnDamnationShutdown);
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

        var cont = GetContractContent(contract);
        if (cont is not InfernalContractData content)
            return InfernalContractValidity.InvalidFormat;
        if (content.Cost > 0)
            return InfernalContractValidity.TooCostly;

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

        var sacrifices = rawContent.GetValueOrDefault("sacrifices");
        var benefits = rawContent.GetValueOrDefault("benefits");
        if (sacrifices == null || sacrifices.Count == 0 || benefits == null || benefits.Count == 0) return null;
        var rawSacrificesGroup = sacrifices[0];
        var rawBenefitsGroup = benefits[0];

        var listSplitterRegex = new Regex("[•\\-\\.\\+]\\s*(.+)"); // bruh

        var rawSacrifices = listSplitterRegex.Matches(rawSacrificesGroup).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        var rawBenefits = listSplitterRegex.Matches(rawBenefitsGroup).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        var rawDamnations = rawSacrifices.Concat(rawBenefits);

        // we now have our string arrays of the wanted effects. Now we need to check them against existing ones.
        // todo check for duplicates
        if (!TryComp<DevilComponent>(contractComp.Author, out var devilComp)) return null;
        var availableDamnations = devilComp.AvailableDamnations.Select(d =>
        {
            _proto.TryIndex<DamnationPrototype>(d, out var damnationProto);
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
            if (_proto.TryIndex<DamnationPrototype>(damnation, out var damnationProto))
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

    private void OnPaperWriteAttempt(EntityUid uid, InfernalContractComponent contractComp, ref PaperWriteAttemptEvent args)
    {
        if(args.Editor != contractComp.Author)
        {
            args.Cancelled = true;
            args.FailReason = Loc.GetString("infernal-contract-edit-fail");
        }
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

        // majority of the damnations wont work on cyborgs, and i dont want to make an edge case for literally everything - lets just stick to humanoids for now...
        // do borgs even have souls? we did recently downgrade them to property damage only to hurt them
        if (HasComp<DevilComponent>(args.Signer) || HasComp<DamnedComponent>(args.Signer) || HasComp<BorgChassisComponent>(args.Signer))
        {
            args.FailReason = Loc.GetString("infernal-contract-popup-fail-self");
            args.Cancelled = true;
            return;
        }

        // ok now we damn
        var contract = GetContractContent(uid);
        if (contract == null) return;
        if (contract?.Damnations.Count == 0) return;

        DamnEntity(args.Signer, (InfernalContractData)contract!, contractComp.Author);

        contractComp.Completed = true;
        Dirty(uid, contractComp);
    }
    #endregion

    #region damnation
    protected bool CanDamn(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto) => !entity.Comp.Damnations.Contains(proto);

    protected bool AddDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto)
    {
        // here we shove all the components in, and then await their potential fails later via the event
        if (!CanDamn(entity, proto)) return false;
        if (!_proto.TryIndex(proto, out var damnationPrototype)) return false;

        EntityManager.AddComponents(entity.Owner, damnationPrototype.Components);
        EntityManager.RemoveComponents(entity.Owner, damnationPrototype.RemovedComponents);

        foreach (var action in damnationPrototype.Actions)
        {
            if (!action.IocResolved)
            {
                action.ResolveIoC();
                action.IocResolved = true;
            }

            if (!action.Action(entity)) return false;
        }

        entity.Comp.NetCost += damnationPrototype.Cost;
        entity.Comp.Damnations.Add(proto);

        return true;
    }

    protected bool DamnEntity(EntityUid ent, InfernalContractData contract, EntityUid devil)
    {
        EnsureComp<DamnedComponent>(ent, out var damnedComp);

        damnedComp.DamnedBy = devil;

        // we add here instead of component startup so that we can know the devil's uid
        if (TryComp<DevilComponent>(devil, out var devilComponent) && contract.Damnations.Contains(devilComponent.SoulDamnation))
        {
            devilComponent.DamnedSouls.Add(ent);

            var ev = new DevilSoulsDamnedCountChangedEvent();
            RaiseLocalEvent(devil, ref ev);
        }

        // check to see that all of the damnations will work, before we try to add any
        foreach (var damnation in contract.Damnations)
        {
            if (!CanDamn((ent, damnedComp), damnation))
            {
                var ev = new DamnationInitFailEvent();
                RaiseLocalEvent(ent, ref ev);
                return false;
            }
        }

        foreach (var damnation in contract.Damnations)
        {
            if(!AddDamnation((ent, damnedComp), damnation))
            {
                var ev = new DamnationInitFailEvent();
                RaiseLocalEvent(ent, ref ev);
                return false;
            }
        }

        _popup.PopupPredicted(Loc.GetString("devil-popup-damnation", ("name", Name(ent))), ent, ent, PopupType.MediumCaution);

        return true;
    }

    protected bool RemoveDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> damnation)
    {
        if (!entity.Comp.Damnations.Contains(damnation)) return false;
        if (!_proto.TryIndex(damnation, out var damnationPrototype)) return false;

        if (damnationPrototype.ReverseOnRemove)
        {
            EntityManager.RemoveComponents(entity.Owner, damnationPrototype.Components);
            EntityManager.AddComponents(entity.Owner, damnationPrototype.RemovedComponents);
        }

        foreach (var action in damnationPrototype.Actions)
        {
            if (!action.IocResolved) {
                action.ResolveIoC();
                action.IocResolved = true;
            }

            action.ReverseAction(entity);
        }

        entity.Comp.Damnations.Remove(damnation);

        return true;
    }

    /// <summary>
    /// If this event is triggered, a damnation has failed to apply, so we need to reverse them all
    /// </summary>
    private void OnDamnationInitFail(Entity<DamnedComponent> ent, ref DamnationInitFailEvent args)
    {
        var damnations = new List<ProtoId<DamnationPrototype>>(ent.Comp.Damnations);
        foreach (var damnation in damnations)
            RemoveDamnation(ent, damnation);
        RemComp<DamnedComponent>(ent.Owner);

        _popup.PopupEntity(Loc.GetString("devil-popup-damnation-fail"), ent.Owner, PopupType.Small);
    }

    private void OnDamnationShutdown(Entity<DamnedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<DevilComponent>(ent.Comp.DamnedBy, out var devilComp)) {
            devilComp.DamnedSouls.Remove(ent.Owner);
            var ev = new DevilSoulsDamnedCountChangedEvent();
            RaiseLocalEvent(ent.Comp.DamnedBy, ref ev);
        }
    }
    #endregion

    #region abilities
    private void OnOpenDamnationsMenu(EntityUid uid, DevilComponent devilComp, ref OpenDamnationsMenuEvent args)
    {
        if (!TryComp<UserInterfaceComponent>(uid, out var userInterfaceComp) || !TryComp<ActorComponent>(uid, out var actorComp)) return;

        var uiState = new DevilDamnationsBuiState(devilComp.AvailableDamnations);
        _userInterface.SetUiState((uid, userInterfaceComp), DamnationsMenuUiKey.Key, uiState);
        _userInterface.TryToggleUi((uid, userInterfaceComp), DamnationsMenuUiKey.Key, actorComp.PlayerSession);
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
