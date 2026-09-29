using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._Starlight.Paper;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Content.Shared.Players;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Shared._Starlight.Devil;

public abstract partial class SharedDevilSystem : EntitySystem
{
    [Dependency] private ParsablePaperSystem _parsablePaper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private SharedPvsOverrideSystem _pvs = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    private Dictionary<ProtoId<DamnationPrototype>, DamnationPrototype> _damnations = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfernalContractComponent, ComponentInit>(OnContractInit);
        SubscribeLocalEvent<InfernalContractComponent, ExaminedEvent>(OnExamineEvent);
        SubscribeLocalEvent<InfernalContractComponent, PaperSignedEvent>(OnSignedEvent);
        SubscribeLocalEvent<InfernalContractComponent, PaperWriteAttemptEvent>(OnPaperWriteAttempt);
        SubscribeLocalEvent<InfernalContractComponent, PaperInputTextMessage>(OnPaperInputTextMessage, after: [typeof(PaperSystem)]);

        SubscribeLocalEvent<DevilComponent, ComponentInit>(OnDevilInit);
        SubscribeLocalEvent<DevilComponent, OpenDamnationsMenuEvent>(OnOpenDamnationsMenu);
        SubscribeLocalEvent<DevilComponent, BoundUIOpenedEvent>(OnBUIOpened);
        SubscribeLocalEvent<DevilComponent, BoundUIClosedEvent>(OnBUIClosed);

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

        if (TryComp<DevilComponent>(contractComp.Author, out var devil))
        {
            foreach (var damnation in content.Damnations)
            {
                var maxUses = _damnations[damnation].MaxUses;
                if (maxUses == -1) continue;
                if (!devil.DamnationUsage.ContainsKey(damnation) && maxUses != 0) continue; // edge case for 0 use
                if (maxUses <= devil.DamnationUsage[damnation])
                    return InfernalContractValidity.OverusedDamnation;
            }
        }

        return InfernalContractValidity.Valid;
    }

    protected InfernalContractData? GetContractContent(EntityUid contract)
    {
        if (!TryComp<InfernalContractComponent>(contract, out var contractComp) || !TryComp<ParsablePaperComponent>(contract, out var parsableComponent))
            return null;

        InfernalContractData data;
        data.Damnations = new();
        data.InvalidDamnations = new();
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
        if (!TryComp<DevilComponent>(contractComp.Author, out var devilComp)) return null;
        var availableDamnations = devilComp.AvailableDamnations.Select(d => _damnations[d].Name.ToLower()).ToList();
        foreach (var damnation in rawDamnations)
        {
            var index = availableDamnations.IndexOf(damnation.ToLower());
            if (index != -1)
                data.Damnations.Add(devilComp.AvailableDamnations[index]);
            else if (!damnation.Equals(contractComp.BlankClauseText))
            {
                // prevent tag injection
                var sanitized = FormattedMessage.EscapeText(damnation);
                data.InvalidDamnations.Add(sanitized.ToLower());
            }
        }

        data.Damnations = data.Damnations.Distinct().ToList();

        foreach (var damnation in data.Damnations)
            data.Cost += _damnations[damnation].Cost;

        return data;
    }

    private void OnExamineEvent(EntityUid uid, InfernalContractComponent contractComp, ref ExaminedEvent args)
    {
        var contractValidity = GetContractValidity(uid);
        if (contractValidity == InfernalContractValidity.NotAContract) return;

        args.PushMarkup(Loc.GetString($"infernal-contract-examined-{contractValidity}"));

        if(GetContractContent(uid) is not InfernalContractData contractData) return;

        args.PushMarkup(Loc.GetString("infernal-contract-examined-cost", ("value", contractData.Cost)));

        if(contractData.InvalidDamnations.Count > 0)
            args.PushMarkup(Loc.GetString("infernal-contract-examined-misspelling",
                ("items", string.Join(", ", contractData.InvalidDamnations))));
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

    /// <summary>
    /// temp alert to announce if there are mispelled damnations until i come back with something more permanent
    /// </summary>
    private void OnPaperInputTextMessage(Entity<InfernalContractComponent> ent, ref PaperInputTextMessage args)
    {
        if (GetContractValidity(ent.Owner) != InfernalContractValidity.Valid)
            return;
        if(GetContractContent(ent.Owner) is not InfernalContractData data)
            return;

        // if the contract has misspellings, we give it the "blank" (dull) sprite, to provide some visual
        // indication that something is slightly off
        if(data.InvalidDamnations.Count > 0)
        {
            _appearance.SetData(ent.Owner, PaperVisuals.Status, PaperStatus.Blank);
            _metadata.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.MispelledContractName));
        }
        else
        {
            _metadata.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.CorrectContractName));
        }
    }

    /// <summary>
    /// for consistency's sake, set name to correct upon spawn - incase someone wants to change this for some reason
    /// </summary>
    private void OnContractInit(Entity<InfernalContractComponent> ent, ref ComponentInit args) => _metadata.SetEntityName(ent.Owner, Loc.GetString(ent.Comp.CorrectContractName));

    #endregion

    #region damnation
    protected bool CanDamn(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto) => !entity.Comp.Damnations.Contains(proto);

    protected bool AddDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto)
    {
        // here we shove all the components in, and then await their potential fails later via the event
        if (!CanDamn(entity, proto)) return false;
        if (!_damnations.ContainsKey(proto)) return false;
        var damnationPrototype = _damnations[proto];

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

        // epic success, now lets log it on the devil
        if (TryComp<DevilComponent>(entity.Comp.DamnedBy, out var devil))
        {
            if (!devil.DamnationUsage.ContainsKey(proto)) devil.DamnationUsage[proto] = 0;
            devil.DamnationUsage[proto]++;
        }

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
            // p = np solved in ss14 (real) (3am challenge)
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
        if (!_damnations.ContainsKey(damnation)) return false;
        var damnationPrototype = _damnations[damnation];

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
    private void OnOpenDamnationsMenu(Entity<DevilComponent> devil, ref OpenDamnationsMenuEvent args)
    {
        if (!TryComp<UserInterfaceComponent>(devil.Owner, out var userInterfaceComp) || !TryComp<ActorComponent>(devil.Owner, out var actorComp)) return;

        var damnationsMeta = devil.Comp.AvailableDamnations.Select(x => (x, devil.Comp.DamnationUsage.GetValueOrDefault(x, 0))).ToList();
        var damnedCrew = new List<(NetEntity, string)>();
        foreach (var uid in devil.Comp.DamnedSouls)
        {
            _entity.TryGetNetEntity(uid, out var netuid);
            if(netuid is { } netEnt) damnedCrew.Add((netEnt, Name(uid)));
        }

        var uiState = new DevilDamnationsBuiState(damnationsMeta, damnedCrew);
        _userInterface.SetUiState((devil.Owner, userInterfaceComp), DamnationsMenuUiKey.Key, uiState);

        _userInterface.TryToggleUi((devil.Owner, userInterfaceComp), DamnationsMenuUiKey.Key, actorComp.PlayerSession);
    }

    /// <summary>
    /// Add PVS overrides for damned players so they don't show up naked on the damned UI.
    /// </summary>
    private void OnBUIOpened(Entity<DevilComponent> devil, ref BoundUIOpenedEvent args)
    {
        if (!_player.TryGetSessionByEntity(devil.Owner, out var session)) return;

        foreach (var uid in devil.Comp.DamnedSouls)
            _pvs.AddSessionOverride(uid, session);
    }
    /// <summary>
    /// Remove the PVS overrides we added when the BUI was opened.
    /// </summary>
    private void OnBUIClosed(Entity<DevilComponent> devil, ref BoundUIClosedEvent args)
    {
        if (!_player.TryGetSessionByEntity(devil.Owner, out var session)) return;

        foreach (var uid in devil.Comp.DamnedSouls)
            _pvs.RemoveSessionOverride(uid, session);
    }

    /// <summary>
    /// Setup damnation map, prevent duplicate proto lookups as this will be happening very frequently.
    /// </summary>
    private void OnDevilInit(Entity<DevilComponent> devil, ref ComponentInit args) =>
        _damnations = _proto.EnumeratePrototypes<DamnationPrototype>().ToDictionary(p => (ProtoId<DamnationPrototype>)p.ID, p => p);
    #endregion
}

public enum InfernalContractValidity
{
    Valid,
    InvalidFormat,
    TooCostly,
    OverusedDamnation,
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

    public List<string> InvalidDamnations;
}
