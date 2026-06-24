using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Actions;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared._Starlight.Samurai;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared._Starlight.CCVar;
using Robust.Server.Audio;
using Content.Shared.Actions.Components;

namespace Content.Server._Starlight.Samurai;

public sealed partial class SamuraiCodesSystem : SharedSamuraiCodeSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private UserInterfaceSystem _bui = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private AudioSystem _audio = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public IReadOnlyList<SamuraiCode> SharedCodes => _sharedCodes.AsReadOnly();
    private readonly List<SamuraiCode> _sharedCodes = [];
    // cached hashset that never gets modified
    private readonly HashSet<SamuraiCode> _emptyCodes = [];
    // cached hashset that gets changed in GetCodeProtoSet
    private readonly HashSet<ProtoId<SamuraiCodePrototype>> _codeProtos = [];

    private readonly ProtoId<DatasetPrototype> _sharedDataset = "SamuraiCodesShared";

    private readonly EntProtoId _actionViewCodes = "ActionViewCodes";

    private readonly ProtoId<WeightedRandomPrototype> _randomSamuraiCodeDataset = "RandomSamuraiCodeDataset";

    public override void Initialize()
    {
        base.Initialize();

        NewSharedCodes();

        SubscribeLocalEvent<SamuraiCodesComponent, MapInitEvent>(OnSamuraiCodeInit);
        SubscribeLocalEvent<SamuraiCodesComponent, ComponentShutdown>(OnSamuraiCodeShutdown);
        SubscribeLocalEvent<SamuraiCodesComponent, ToggleCodesScreenEvent>(OnToggleCodesScreen);
        SubscribeLocalEvent<SamuraiCodesComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
        SubscribeLocalEvent<RoundRestartCleanupEvent>((_) => NewSharedCodes());
    }

    public void NewSharedCodes()
    {
        _sharedCodes.Clear();
        for (int i = 0; i < _config.GetCVar(ImpCCVars.SamuraiSharedCodeCount); i++)
            TryAddSharedCode(notify: false); // don't spam notify if there are multiple codes

        NotifySharedCodeChange();
    }

    public bool TryAddSharedCode(SamuraiCode? code = null, bool checkConflicts = true, bool notify = true)
    {
        if (code == null)
        {
            if (!TryPick(_sharedDataset, out var codeProto, _sharedCodes))
                return false;

            code = RollCode(codeProto);
            checkConflicts = false; // TryPick has cleared this code already
        }

        if (checkConflicts && SharedCodeConflicts(code))
            return false;

        _sharedCodes.Add(code);

        if (notify)
            NotifySharedCodeChange();

        return true;
    }

    private bool SharedCodeConflicts(SamuraiCode code)
        => code.ProtoId is { } id &&
            (GetConflicts(_sharedCodes).Contains(id) ||
            GetCodeProtoSet(_sharedCodes).Overlaps(code.Conflicts));

    private void NotifySharedCodeChange()
    {
        var query = EntityQueryEnumerator<SamuraiCodesComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.FollowsSharedCodes)
                continue;

            NotifyCodeChange((uid, comp));
        }
    }

    private void OnBoundUIOpened(Entity<SamuraiCodesComponent> ent, ref BoundUIOpenedEvent args)
        => UpdateBUIState(ent);

    private void OnToggleCodesScreen(Entity<SamuraiCodesComponent> ent, ref ToggleCodesScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;

        _bui.TryToggleUi(ent.Owner, SamuraiCodesUiKey.Key, actor.PlayerSession);
    }

    public bool TryPick(ProtoId<DatasetPrototype> datasetProto, [NotNullWhen(true)] out SamuraiCodePrototype? proto, IEnumerable<SamuraiCode>? currentCodes = null, HashSet<ProtoId<SamuraiCodePrototype>>? conflicts = null)
    {
        var dataset = _proto.Index<DatasetPrototype>(datasetProto);
        var choices = dataset.Values.ToList();

        currentCodes ??= _emptyCodes;
        conflicts ??= GetConflicts(currentCodes);

        var currentCodeProtos = GetCodeProtoSet(currentCodes);

        while (choices.Count > 0)
        {
            var codeId = _random.PickAndTake(choices);
            if (conflicts.Contains(codeId))
                continue; // Skip proto if an existing code conflicts with it

            var codeProto = _proto.Index<SamuraiCodePrototype>(codeId);
            if (codeProto.Conflicts.Overlaps(currentCodeProtos))
                continue; // Skip proto if it conflicts with an existing code

            proto = codeProto;
            return true;
        }

        proto = null;
        return false;
    }

    /// <summary>
    /// Send the player a audiovisual notification and update the codes UI.
    /// </summary>
    public void NotifyCodeChange(Entity<SamuraiCodesComponent> ent)
    {
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var session = actor.PlayerSession;
        _audio.PlayGlobal(ent.Comp.CodesChangedSound, session);

        var msg = Loc.GetString("samurai-codes-update-notify");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, session.Channel, colorOverride: Color.Orange);

        // update the UI without needing to re-open it
        UpdateBUIState(ent);
    }

    public void UpdateBUIState(Entity<SamuraiCodesComponent> ent)
    {
        var state = new SamuraiCodesBuiState(ent.Comp.FollowsSharedCodes ? _sharedCodes : []);
        _bui.SetUiState(ent.Owner, SamuraiCodesUiKey.Key, state);
    }

    /// <summary>
    /// Directly add a code to a Samurai, ignoring conflicts.
    /// </summary>
    public void AddCode(Entity<SamuraiCodesComponent> ent, SamuraiCode code, bool notify = true)
    {
        ent.Comp.Codes.Add(code);
        Dirty(ent);

        if (notify)
            NotifyCodeChange(ent);
        else // NotifyCodeChange will update UI so this is in else
            UpdateBUIState(ent);
    }

    /// <summary>
    /// Creates a SamuraiCode instance from the given SamuraiCodePrototype, and rolls
    /// its code vars.
    /// </summary>
    public SamuraiCode RollCode(SamuraiCodePrototype proto)
    {
        var code = proto.ShallowClone();
        var alreadyChosen = new HashSet<ProtoId<SamuraiCodePrototype>>();

        foreach (var (name, datasetID) in proto.CodeVarDatasets)
        {
            var dataset = _proto.Index(datasetID);

            if (proto.AllowDuplicateCodeVars)
            {
                code.CodeVars.Add(name, _random.Pick(dataset));
                continue;
            }

            var choices = dataset.Values.ToList();
            var foundChoice = false;
            while (choices.Count > 0)
            {
                var choice = _random.PickAndTake(choices);
                if (alreadyChosen.Contains(choice) || code.CodeVars.ContainsValue(choice))
                    continue;

                code.CodeVars.TryAdd(name, choice);
                alreadyChosen.Add(choice);
                foundChoice = true;
                break;
            }

            if (!foundChoice)
            {
                Log.Warning($"Ran out of choices for codevar \"{name}\" in \"{proto.ID}\"! Cant pick duplicates.");
            }
        }

        return code;
    }

    /// <summary>
    /// Checks if the given code prototype conflicts with the current codes, and
    /// adds the code if it does not.
    /// </summary>
    public bool TryAddCode(Entity<SamuraiCodesComponent> ent, SamuraiCodePrototype codeProto, bool allowConflict = false, bool notify = true)
    {
        if (!allowConflict && GetConflicts(ent).Contains(codeProto.ID))
            return false;

        AddCode(ent, RollCode(codeProto), notify);
        return true;
    }

    /// <summary>
    /// Checks if the given code prototype conflicts with the current codes, and
    /// adds the code if it does not.
    /// </summary>
    public bool TryAddCode(Entity<SamuraiCodesComponent> ent, ProtoId<SamuraiCodePrototype> codeProto, bool allowConflict = false, bool notify = true)
        => TryAddCode(ent, _proto.Index(codeProto), allowConflict, notify);

    /// <summary>
    /// Tries to add a random code using a specific dataset.
    /// </summary>
    public bool TryAddRandomCode(Entity<SamuraiCodesComponent> ent, string datasetProto, bool notify = true)
    {
        if (TryPick(datasetProto, out var codeProto, GetActiveCodes(ent)))
        {
            AddCode(ent, RollCode(codeProto), notify);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to add a random code using <see cref="_randomSamuraiCodeDataset"/>.
    /// </summary>
    public bool TryAddRandomCode(Entity<SamuraiCodesComponent> ent, bool notify = true)
    {
        var datasetProto = _proto.Index(_randomSamuraiCodeDataset).Pick();
        return TryAddRandomCode(ent, datasetProto, notify);
    }

    /// <summary>
    /// Set the codes for a samurai directly.
    /// This does NOT check conflicts so be careful with what you set!
    /// </summary>
    public void SetCodes(Entity<SamuraiCodesComponent> ent, IEnumerable<SamuraiCode> codes, bool notify = true)
    {
        ent.Comp.Codes = codes.ToList();
        Dirty(ent);

        if (notify)
            NotifyCodeChange(ent);
        else
            UpdateBUIState(ent);
    }

    public HashSet<ProtoId<SamuraiCodePrototype>> GetConflicts(IEnumerable<SamuraiCode> codes)
    {
        var conflicts = new HashSet<ProtoId<SamuraiCodePrototype>>();

        foreach (var code in codes)
        {
            if (code.ProtoId is {} id)
                conflicts.Add(id); // Specific codes shouldn't be added twice
            conflicts.UnionWith(code.Conflicts);
        }

        return conflicts;
    }

    /// <summary>
    /// Get the conflicts for a samurai's active codes.
    /// </summary>
    public HashSet<ProtoId<SamuraiCodePrototype>> GetConflicts(Entity<SamuraiCodesComponent> ent)
        // TODO: Should probably cache this when codes get updated
        => GetConflicts(GetActiveCodes(ent));

    /// <summary>
    /// Maps some codes to their ids.
    /// The hashset returned is reused and so you must not modify it.
    /// </summary>
    public HashSet<ProtoId<SamuraiCodePrototype>> GetCodeProtoSet(IEnumerable<SamuraiCode> codes)
    {
        _codeProtos.Clear();
        foreach (var code in codes)
        {
            if (code.ProtoId is {} id)
                _codeProtos.Add(id);
        }

        return _codeProtos;
    }

    /// <summary>
    /// Return a list of the codes that are affecting this entity.
    /// </summary>
    public List<SamuraiCode> GetActiveCodes(Entity<SamuraiCodesComponent> ent, bool includeShared = true)
    {
        if (includeShared && ent.Comp.FollowsSharedCodes)
            return new List<SamuraiCode>(SharedCodes.Concat(ent.Comp.Codes));

        return ent.Comp.Codes;
    }

    public void RemoveCode(Entity<SamuraiCodesComponent> ent, int index, bool notify = true)
    {
        var codes = ent.Comp.Codes;
        if (codes.Count <= index)
            return;

        ent.Comp.Codes.RemoveAt(index);

        if (notify)
            NotifyCodeChange(ent);
    }

    private void OnSamuraiCodeInit(Entity<SamuraiCodesComponent> ent, ref MapInitEvent args)
    {

        foreach (var dataset in ent.Comp.CodeDatasets)
        {
            if (TryPick(dataset, out var code, GetActiveCodes(ent)))
                TryAddCode(ent, code, true, false);
        }

        ent.Comp.Action = _actions.AddAction(ent.Owner, _actionViewCodes);
    }

    private void OnSamuraiCodeShutdown(Entity<SamuraiCodesComponent> ent, ref ComponentShutdown args)
    {
        var act = ent.Comp.Action;
        if (TryComp<ActionComponent>(act, out var action))
            _actions.RemoveAction(act);
    }

    protected override void OnEmagged(Entity<SamuraiCodesComponent> ent, ref GotEmaggedEvent args)
    {
        base.OnEmagged(ent, ref args);
        if (!args.Handled)
            return;

        TryAddRandomCode(ent, ent.Comp.Wildcard);
    }

    // Tries to add a code on an ion storm event
    public void OnIonStorm(Entity<SamuraiCodesComponent> ent)
    {
        if (!_random.Prob(ent.Comp.IonStormCodeChance))
            return;
        TryAddRandomCode(ent, ent.Comp.Wildcard);
    }
}
