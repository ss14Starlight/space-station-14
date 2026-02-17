using System.Linq;
using Content.Server._Starlight.Plumbing.Components;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
///     Handles the plumbing smart dispenser: pulls all reagents from the network,
///     stores up to a per-reagent cap, and fills labeled jugs on interaction.
/// </summary>
[UsedImplicitly]
public sealed class PlumbingSmartDispenserSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

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

        SubscribeLocalEvent<PlumbingSmartDispenserComponent, PlumbingDeviceUpdateEvent>(OnDeviceUpdate);
        SubscribeLocalEvent<PlumbingSmartDispenserComponent, PlumbingPullIntoAttemptEvent>(OnPullInto);
        SubscribeLocalEvent<PlumbingSmartDispenserComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlumbingSmartDispenserComponent, AfterActivatableUIOpenEvent>(OnUIOpened);

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

    private void OnDeviceUpdate(Entity<PlumbingSmartDispenserComponent> ent, ref PlumbingDeviceUpdateEvent args)
    {
        UpdateUiState(ent);
    }

    /// <summary>
    /// Caps or denies pulls for reagents that are at or near the per-reagent limit.
    /// </summary>
    private void OnPullInto(Entity<PlumbingSmartDispenserComponent> ent, ref PlumbingPullIntoAttemptEvent args)
    {
        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
            return;

        var current = solution.GetReagentQuantity(new ReagentId(args.ReagentPrototype, null));
        var room = ent.Comp.MaxPerReagent - current;

        if (room <= FixedPoint2.Zero)
        {
            args.Cancelled = true;
            return;
        }

        if (room < args.MaxAllowed)
            args.MaxAllowed = room;
    }

    private void OnInteractUsing(Entity<PlumbingSmartDispenserComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<LabelComponent>(args.Used, out var label) || string.IsNullOrEmpty(label.CurrentLabel))
        {
            _popup.PopupEntity(Loc.GetString("plumbing-smart-dispenser-no-label"), ent.Owner, args.User);
            return;
        }

        if (!TryMatchLabelToReagent(label.CurrentLabel, out var reagentId))
        {
            _popup.PopupEntity(Loc.GetString("plumbing-smart-dispenser-no-match"), ent.Owner, args.User);
            return;
        }

        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var fridgeSolnEnt, out var fridgeSolution))
            return;

        var available = fridgeSolution.GetReagentQuantity(new ReagentId(reagentId, null));

        if (available <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("plumbing-smart-dispenser-not-in-stock",
                ("reagent", _prototypeManager.Index<ReagentPrototype>(reagentId).LocalizedName)),
                ent.Owner, args.User);
            return;
        }

        if (!_solutionSystem.TryGetRefillableSolution(args.Used, out var jugSolnEnt, out var jugSolution))
            return;

        var jugAvailable = jugSolution.AvailableVolume;
        if (jugAvailable <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("plumbing-smart-dispenser-jug-full"), ent.Owner, args.User);
            return;
        }

        var transferAmount = FixedPoint2.Min(available, jugAvailable);
        var removed = _solutionSystem.RemoveReagent(fridgeSolnEnt.Value, new ReagentId(reagentId, null), transferAmount);

        if (removed > FixedPoint2.Zero)
        {
            _solutionSystem.TryAddReagent(jugSolnEnt.Value, reagentId, removed, out _);

            var reagentName = _prototypeManager.Index<ReagentPrototype>(reagentId).LocalizedName;
            _popup.PopupEntity(Loc.GetString("plumbing-smart-dispenser-filled",
                ("reagent", reagentName),
                ("amount", removed)),
                ent.Owner, args.User);
        }

        args.Handled = true;
        UpdateUiState(ent);
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

        Log.Debug($"SmartFridge label match: raw='{label}' stripped='{stripped}' token='{token}'");

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
        Log.Debug($"SmartFridge matched token='{token}' to reagent='{reagentId}'");
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

        return -(prefixLen * 100000) + (lastMatchPos - firstMatchPos) * 1000 + name.Length;
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

    private void OnUIOpened(Entity<PlumbingSmartDispenserComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUiState(ent);
    }

    private void UpdateUiState(Entity<PlumbingSmartDispenserComponent> ent)
    {
        var entries = new List<PlumbingSmartDispenserReagentEntry>();

        if (_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
        {
            foreach (var reagent in solution.Contents)
            {
                if (reagent.Quantity <= FixedPoint2.Zero)
                    continue;

                if (!_prototypeManager.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var proto))
                    continue;

                entries.Add(new PlumbingSmartDispenserReagentEntry(
                    proto.ID,
                    proto.LocalizedName,
                    reagent.Quantity,
                    proto.SubstanceColor));
            }

            // Sort alphabetically by localized name for consistent display
            entries.Sort((a, b) => string.Compare(a.LocalizedName, b.LocalizedName, StringComparison.OrdinalIgnoreCase));
        }

        var state = new PlumbingSmartDispenserBuiState(entries, ent.Comp.MaxPerReagent.Float());
        _uiSystem.SetUiState(ent.Owner, PlumbingSmartDispenserUiKey.Key, state);
    }
}
