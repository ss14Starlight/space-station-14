using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Plumbing.EntitySystems;

public abstract partial class SharedPlumbingSmartDispenserSystem : EntitySystem
{
    [Dependency] protected SharedHandsSystem _hands = default!;
    [Dependency] protected InjectorSystem _injectorSystem = default!;
    [Dependency] protected SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] protected SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] protected SharedPopupSystem _popup = default!;
    [Dependency] protected SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] protected AccessReaderSystem _accessReader = default!;
    [Dependency] protected ActionBlockerSystem _actionBlocker = default!;
    [Dependency] protected IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// Cached mapping of label prefix (lowercase) → reagent prototype ID.
    /// Built lazily on first use and invalidated on prototype reload.
    /// </summary>
    private Dictionary<string, string>? _labelCache;

    /// <summary>
    /// Cached sorted list of all reagent localized names for prefix matching.
    /// </summary>
    private List<(string LocalizedName, string PrototypeId)>? _reagentNames;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<PlumbingSmartDispenserComponent>(PlumbingSmartDispenserUiKey.Key, subs =>
        {
            subs.Event<PlumbingSmartDispenserDispenseReagentMessage>(OnDispenseReagentMessage);
            subs.Event<PlumbingSmartDispenserClearSolutionMessage>(OnClearContainerMessage);
        });

        _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _prototypeManager.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        // Invalidate cache so it gets rebuilt with new reagent names
        _labelCache = null;
        _reagentNames = null;
    }

    protected virtual void UpdateUi(Entity<PlumbingSmartDispenserComponent> ent)
    {
    }

    protected virtual void SetInteractedWith(Entity<PlumbingSmartDispenserComponent> ent, EntityUid? container)
    {
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<PlumbingSmartDispenserComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<HandLabelerComponent>(args.Used))
        {
            args.Handled = true;
            SetInteractedWith(ent, args.Used);
            _uiSystem.OpenUi(ent.Owner, PlumbingSmartDispenserUiKey.Key, args.User);
            return;
        }

        if (TryComp<LabelComponent>(args.Used, out var label)
            && !string.IsNullOrWhiteSpace(label.CurrentLabel)
            && TryMatchLabelToReagent(label.CurrentLabel, out var reagentId))
        {
            if (_powerReceiver.IsPowered(ent.Owner))
            {
                args.Handled = true;

                TryDispenseReagent(ent, reagentId, args.Used, null, args.User, true);
                UpdateUi(ent);
                return;
            }
            else
            {
                // smart functionality requires power, don't return so we get the normal manual dispense UI
                _popup.PopupPredicted(Loc.GetString("plumbing-smart-dispenser-not-powered"), ent.Owner, args.User);
            }
        }

        if (GetFillSolutionFromContainer(args.Used) is null)
            return;

        args.Handled = true;

        SetInteractedWith(ent, args.Used);

        _uiSystem.OpenUi(ent.Owner, PlumbingSmartDispenserUiKey.Key, args.User);
    }

    private void OnDispenseReagentMessage(Entity<PlumbingSmartDispenserComponent> ent, ref PlumbingSmartDispenserDispenseReagentMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        if (GetBoundLabeler(actor, args.TargetEntity) is Entity<HandLabelerComponent> labeler)
        {
            SetReagentLabel(ent, args.ReagentId, labeler.Comp.AssignedLabel, actor);
            UpdateUi(ent);
            return;
        }

        var outputContainer = GetBoundContainer(actor, args.TargetEntity);
        if (outputContainer is not { } targetContainer)
        {
            _popup.PopupPredicted(Loc.GetString("plumbing-smart-dispenser-no-container"), ent.Owner, actor);
            UpdateUi(ent);

            return;
        }

        TryDispenseReagent(
            ent,
            args.ReagentId,
            targetContainer,
            args.Amount,
            actor,
            true);

        UpdateUi(ent);
    }

    private void OnClearContainerMessage(Entity<PlumbingSmartDispenserComponent> ent, ref PlumbingSmartDispenserClearSolutionMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        if (!CanInteract(ent, actor))
            return;

        var outputContainer = GetBoundContainer(actor, args.TargetEntity);
        if (outputContainer is not { } targetContainer)
            return;

        if (!_solutionSystem.TryGetFitsInDispenser(targetContainer, out var targetEnt, out _)
            && !_solutionSystem.TryGetRefillableSolution(targetContainer, out targetEnt, out _)
            && (!TryComp<InjectorComponent>(targetContainer, out var injector)
                || !TryComp<SolutionManagerComponent>(targetContainer, out var manager)
                || !_solutionSystem.TryGetSolution((targetContainer, manager), injector.SolutionName, out targetEnt, out _)))
            return;

        _solutionSystem.RemoveAllSolution(targetEnt.Value);
        UpdateUi(ent);
    }

    /// <summary>
    /// Attempts to match a label string to a reagent prototype ID.
    /// Labels can contain rich text markup and abbreviations like ":[color=#ffaa00]BIC|BRT|15/5u".
    /// The algorithm strips markup, skips leading punctuation, extracts the first alphabetic token,
    /// then tries: 1) prefix match, 2) ordered subsequence match against all reagent names.
    /// Results are cached for performance.
    /// </summary>
    private bool TryMatchLabelToReagent(string label, out string reagentId)
    {
        reagentId = string.Empty;

        _labelCache ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_labelCache.TryGetValue(label, out var cached))
        {
            reagentId = cached;
            return true;
        }

        var stripped = StripBrackets(label);
        var token = ExtractFirstAlphaToken(stripped);

        if (string.IsNullOrEmpty(token) || token.Length < 2)
            return false;

        _reagentNames ??= BuildReagentNameList();

        // Pass 1: Prefix match (covers exact matches too). Shortest name wins.
        string? bestMatch = null;
        var bestScore = int.MaxValue;

        foreach (var (name, protoId) in _reagentNames)
        {
            if (name.Length >= token.Length
                && name.StartsWith(token, StringComparison.OrdinalIgnoreCase)
                && name.Length < bestScore)
            {
                bestMatch = protoId;
                bestScore = name.Length;
            }
        }

        // Pass 2: Ordered subsequence fallback. Strip spaces from token first.
        if (bestMatch == null)
        {
            var compactToken = token.Replace(" ", "");
            bestScore = int.MaxValue;

            foreach (var (name, protoId) in _reagentNames)
            {
                var score = ScoreSubsequenceMatch(compactToken, name);
                if (score != int.MinValue && score < bestScore)
                {
                    bestMatch = protoId;
                    bestScore = score;
                }
            }
        }

        if (bestMatch == null)
            return false;

        reagentId = bestMatch;
        _labelCache[label] = reagentId;
        return true;
    }

    /// <summary>
    /// Removes all bracket-enclosed blocks (e.g. [color=#ffaa00]) from a string.
    /// </summary>
    private static string StripBrackets(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        var inBracket = false;

        foreach (var c in text)
        {
            if (c == '[')
                inBracket = true;
            else if (c == ']')
                inBracket = false;
            else if (!inBracket)
                sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Scores an ordered subsequence match. Each char in token must appear in order in the name.
    /// Primary score: consecutive prefix length (more = better).
    /// Secondary: match span (compact = better). Tertiary: name length (shorter = better).
    /// Returns a score (lower = better), or -1 if no match.
    /// </summary>
    private static int ScoreSubsequenceMatch(string token, string name)
    {
        var tokenIdx = 0;
        var firstMatchPos = -1;
        var lastMatchPos = -1;
        var prefixLen = 0;
        var countingPrefix = true;

        for (var i = 0; i < name.Length && tokenIdx < token.Length; i++)
        {
            if (char.ToLowerInvariant(name[i]) == char.ToLowerInvariant(token[tokenIdx]))
            {
                if (firstMatchPos == -1)
                    firstMatchPos = i;
                lastMatchPos = i;

                if (countingPrefix)
                    prefixLen++;

                tokenIdx++;
            }
            else if (countingPrefix)
            {
                countingPrefix = false;
            }
        }

        if (tokenIdx < token.Length)
            return int.MinValue;

        return -(prefixLen * 100000) + ((lastMatchPos - firstMatchPos) * 1000) + name.Length;
    }

    /// <summary>
    /// Extracts the first run of letters (and internal spaces) from a string,
    /// skipping leading non-letter characters. Stops at non-letter, non-space characters.
    /// E.g. ":BIC|BRT" → "BIC", "dexalin plus" → "dexalin plus".
    /// </summary>
    private static string ExtractFirstAlphaToken(string text)
    {
        var start = -1;
        var end = -1;

        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetter(text[i]))
            {
                if (start == -1)
                    start = i;
                end = i + 1;
            }
            else if (start != -1 && text[i] == ' ')
            {
                // Allow spaces between letters
            }
            else if (start != -1)
            {
                break;
            }
        }

        if (start == -1)
            return string.Empty;

        return text[start..end].TrimEnd();
    }

    /// <summary>
    /// Builds a sorted list of (localizedName, prototypeId) for all reagent prototypes.
    /// Sorted by name length ascending so shorter (more specific) names are checked first.
    /// </summary>
    private List<(string LocalizedName, string PrototypeId)> BuildReagentNameList()
    {
        var list = new List<(string, string)>();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<ReagentPrototype>())
        {
            list.Add((proto.LocalizedName, proto.ID));
        }

        // Sort by name length so shorter matches win as tiebreaker
        list.Sort((a, b) => a.Item1.Length.CompareTo(b.Item1.Length));
        return list;
    }

    private void SetReagentLabel(
        Entity<PlumbingSmartDispenserComponent> ent,
        string reagentId,
        string newLabel,
        EntityUid actor)
    {
        if (!CanInteract(ent, actor))
            return;

        // check we actually have the reagent in stock so clients can't
        // fill the label dictionary with infinite entries
        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var sourceEnt, out var sourceSolution)
            || sourceEnt is not { } sourceSolutionEnt)
            return;

        bool hasReagent = false;
        foreach (var reagent in sourceSolution.Contents)
        {
            if (reagent.Reagent.Prototype == reagentId)
            {
                hasReagent = true;
                break;
            }
        }

        if (hasReagent)
        {
            ent.Comp.ReagentLabels[reagentId] = newLabel;
            Dirty(ent);
        }
    }

    private bool TryDispenseReagent(
        Entity<PlumbingSmartDispenserComponent> ent,
        string reagentId,
        EntityUid targetContainer,
        FixedPoint2? requestedAmount,
        EntityUid? user,
        bool showPopup)
    {
        if (user is { Valid : true } && !CanInteract(ent, user.Value))
            return false;

        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var sourceEnt, out var sourceSolution))
            return false;

        if (sourceEnt is not { } sourceSolutionEnt)
            return false;

        ReagentQuantity? sourceReagent = null;

        foreach (var reagent in sourceSolution.Contents)
        {
            if (reagent.Reagent.Prototype != reagentId)
                continue;

            sourceReagent = reagent;
            break;
        }

        var available = sourceReagent?.Quantity ?? FixedPoint2.Zero;

        if (available <= FixedPoint2.Zero)
        {
            if (showPopup && user is { Valid: true } &&
                _prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var proto))
            {
                _popup.PopupPredicted(
                    Loc.GetString("plumbing-smart-dispenser-not-in-stock",
                        ("reagent", proto.LocalizedName)),
                    ent.Owner,
                    user.Value);
            }

            return false;
        }

        if (sourceReagent is not { } sourceReagentValue)
            return false;

        if (TryComp<RefillReagentFilterComponent>(targetContainer, out var filter)
            && !filter.Reagents.Contains(reagentId))
        {
            // Incorrect reagents being put into our lovely automenders (and anything with filters)!
            if (showPopup && user is { Valid: true })
                _popup.PopupPredicted(Loc.GetString(filter.Popup), ent.Owner, user.Value);

            return false;
        }

        if (!_solutionSystem.TryGetFitsInDispenser(targetContainer, out var targetEnt, out var targetSolution)
            && !_solutionSystem.TryGetRefillableSolution(targetContainer, out targetEnt, out targetSolution)
            && (!TryComp<InjectorComponent>(targetContainer, out var injector)
                || !TryComp<SolutionManagerComponent>(targetContainer, out var manager)
                || !_solutionSystem.TryGetSolution((targetContainer, manager), injector.SolutionName, out targetEnt, out targetSolution)))
        {
            return false;
        }

        var transferAmount = FixedPoint2.Min(available, targetSolution!.AvailableVolume);
        if (requestedAmount is { } requested)
            transferAmount = FixedPoint2.Min(transferAmount, requested);

        if (transferAmount <= FixedPoint2.Zero)
        {
            if (showPopup && user is { Valid: true })
                _popup.PopupPredicted(Loc.GetString("plumbing-smart-dispenser-jug-full"), ent.Owner, user.Value);

            return false;
        }

        var removed = _solutionSystem.RemoveReagent(sourceSolutionEnt, sourceReagentValue.Reagent, transferAmount);
        if (removed <= FixedPoint2.Zero)
            return false;

        _solutionSystem.TryAddReagent(targetEnt!.Value, sourceReagentValue.Reagent, removed, out var accepted);

        if (showPopup && accepted > FixedPoint2.Zero && user is { Valid: true } &&
            _prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var reagentProto))
        {
            _popup.PopupPredicted(
                Loc.GetString("plumbing-smart-dispenser-filled",
                    ("reagent", reagentProto.LocalizedName),
                    ("amount", accepted)),
                ent.Owner,
                user.Value);
        }

        if (accepted > FixedPoint2.Zero
            && user is { Valid: true } validUser
            && TryComp<InjectorComponent>(targetContainer, out var injectorComp))
        {
            if (_solutionSystem.TryGetSolution(targetContainer, injectorComp.SolutionName, out _, out var injectorSolution)
                && injectorSolution.AvailableVolume == 0
                && _prototypeManager.Resolve(injectorComp.ActiveModeProtoId, out InjectorModePrototype? activeMode)
                && !activeMode.Behavior.HasFlag(InjectorBehavior.Dynamic))
            {
                _injectorSystem.ToggleMode((targetContainer, injectorComp), validUser);
            }
        }

        return accepted > FixedPoint2.Zero;
    }

    private bool CanInteract(Entity<PlumbingSmartDispenserComponent> ent, EntityUid actor)
    {
        if (!_actionBlocker.CanInteract(actor, ent.Owner) || !_actionBlocker.CanComplexInteract(actor))
            return false;

        // if it has an access reader, access is required
        if (!_accessReader.IsAllowed(actor, ent.Owner))
        {
            _popup.PopupPredicted(Loc.GetString("plumbing-smart-dispenser-access-denied"), ent.Owner, actor);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the solution that would be filled by the dispenser from a given entity.
    /// </summary>
    public Solution? GetFillSolutionFromContainer(EntityUid? container)
    {
        if (container is not { Valid: true })
            return null;

        if (_solutionSystem.TryGetFitsInDispenser(container.Value, out _, out var solution)
            || _solutionSystem.TryGetRefillableSolution(container.Value, out _, out solution)
            || (TryComp<InjectorComponent>(container.Value, out var injector)
                && TryComp<SolutionManagerComponent>(container.Value, out var manager)
                && _solutionSystem.TryGetSolution((container.Value, manager), injector.SolutionName, out _, out solution)))
            return solution;

        return null;
    }

    private Entity<HandLabelerComponent>? GetBoundLabeler(EntityUid actor, NetEntity suppliedEnt)
    {
        if (!TryGetEntity(suppliedEnt, out var ent)
            || ent is not { Valid: true } labeler
            || !_hands.IsHolding(actor, labeler, out _)
            || !TryComp<HandLabelerComponent>(labeler, out var comp))
            return null;

        return (labeler, comp);
    }

    private EntityUid? GetBoundContainer(EntityUid actor, NetEntity suppliedEnt)
    {
        // the target entity is clientsided, because different people can use the dispenser at once
        // but we still do the same is holding check on the server, to make sure that malicious packets
        // can't just bluespace chems into anything from a dispenser

        if (!TryGetEntity(suppliedEnt, out var ent) || ent is not { Valid : true } container)
            return null;

        if (_hands.IsHolding(actor, container, out _))
            return container;

        return null;
    }
}
