using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared._CD.Records;
using Robust.Server.GameObjects;
using Robust.Shared.Log;

namespace Content.Server._CD.Records.Consoles;

/// <summary>
/// Drives the BUI for the Cosmatic Drift record consoles.
/// </summary>
public sealed class CharacterRecordConsoleSystem : EntitySystem
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("characterrecords.console");

    [Dependency] private readonly CharacterRecordsSystem _characterRecords = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CharacterRecordConsoleComponent, CharacterRecordsModifiedEvent>((uid, component, _) =>
            UpdateUi(uid, component));

        Subs.BuiEvents<CharacterRecordConsoleComponent>(CharacterRecordConsoleKey.Key,
            subscriber =>
            {
                subscriber.Event<BoundUIOpenedEvent>((uid, component, _) => UpdateUi(uid, component));
                subscriber.Event<CharacterRecordConsoleSelectMsg>(OnKeySelect);
                subscriber.Event<CharacterRecordsConsoleFilterMsg>(OnFilterApplied);
            });
    }

    private void OnFilterApplied(Entity<CharacterRecordConsoleComponent> ent, ref CharacterRecordsConsoleFilterMsg msg)
    {
        ent.Comp.Filter = msg.Filter;
        UpdateUi(ent);
    }

    private void OnKeySelect(Entity<CharacterRecordConsoleComponent> ent, ref CharacterRecordConsoleSelectMsg msg)
    {
        ent.Comp.SelectedIndex = msg.CharacterRecordKey;
        UpdateUi(ent);
    }

    /// <summary>
    /// Rebuilds the console UI state, ensuring selections stay valid after filters or data changes.
    /// </summary>
    private void UpdateUi(EntityUid entity, CharacterRecordConsoleComponent? console = null)
    {
        if (!Resolve(entity, ref console))
            return;

        var station = _station.GetOwningStation(entity);
        // When the console is not tied to a valid station datastore, fall back to an empty UI state.
        if (!HasComp<StationRecordsComponent>(station) ||
            !HasComp<CharacterRecordsComponent>(station))
        {
            SendState(entity, new CharacterRecordConsoleState { ConsoleType = console.ConsoleType });
            return;
        }

        var characterRecords = _characterRecords.QueryRecords(station.Value);
        var filteredRecords = new List<(uint Key, CharacterRecordConsoleState.CharacterInfo Info, FullCharacterRecords Record)>();

        foreach (var (key, record) in characterRecords)
        {
            if (console.Filter != null && IsSkippedRecord(console.Filter, record))
                continue;

            string displayName;
            if (console.ConsoleType != RecordConsoleType.Admin)
            {
                displayName = $"{record.Name} ({record.JobTitle})";
            }
            else if (record.Owner != null)
            {
                var netEntity = _entityManager.GetNetEntity(record.Owner.Value);
                displayName = $"{record.Name} ({netEntity}, {record.JobTitle})";
            }
            else
            {
                displayName = $"{record.Name} ({record.JobTitle})";
            }

            var info = new CharacterRecordConsoleState.CharacterInfo
            {
                CharacterDisplayName = displayName,
                StationRecordKey = record.StationRecordsKey,
            };

            filteredRecords.Add((key, info, record));
        }

        var listing = new Dictionary<uint, CharacterRecordConsoleState.CharacterInfo>();
        foreach (var entry in filteredRecords)
        {
            if (!listing.TryAdd(entry.Key, entry.Info))
            {
                Sawmill.Error($"Duplicate character record key {entry.Key} for console {ToPrettyString(entity)}.");
            }
        }

        uint? selectedIndex = console.SelectedIndex;
        if (selectedIndex == null || !listing.ContainsKey(selectedIndex.Value))
        {
            if (filteredRecords.Count > 0)
            {
                var fallback = filteredRecords
                    .OrderBy(r => r.Info.CharacterDisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .First();
                selectedIndex = fallback.Key;
                console.SelectedIndex = selectedIndex;
                Sawmill.Debug($"Auto-selected character record {selectedIndex} for console {ToPrettyString(entity)}.");
            }
            else
            {
                selectedIndex = null;
                console.SelectedIndex = null;
            }
        }

        FullCharacterRecords? selectedRecord = null;
        if (selectedIndex is { } idx)
        {
            foreach (var entry in filteredRecords)
            {
                if (entry.Key != idx)
                    continue;

                selectedRecord = entry.Record;
                break;
            }

            if (selectedRecord == null)
            {
                Sawmill.Warning($"Console {ToPrettyString(entity)} references missing character record id {idx}; clearing selection.");
                selectedIndex = null;
                console.SelectedIndex = null;
            }
        }

        (SecurityStatus, string?)? securityStatus = null;
        CriminalRecord? selectedCriminalRecord = null;
        if (selectedRecord != null
            && (console.ConsoleType == RecordConsoleType.Admin || console.ConsoleType == RecordConsoleType.Security)
            && selectedRecord.StationRecordsKey != null)
        {
            var key = new StationRecordKey(selectedRecord.StationRecordsKey.Value, station.Value);
            if (_records.TryGetRecord<CriminalRecord>(key, out var entry))
            {
                securityStatus = (entry.Status, entry.Reason);
                selectedCriminalRecord = entry;
            }
        }

        SendState(entity,
            new CharacterRecordConsoleState
            {
                ConsoleType = console.ConsoleType,
                CharacterList = listing,
                SelectedIndex = selectedIndex,
                SelectedRecord = selectedRecord,
                Filter = console.Filter,
                SelectedSecurityStatus = securityStatus,
                SelectedCriminalRecord = selectedCriminalRecord,
            });
    }

    private void SendState(EntityUid entity, CharacterRecordConsoleState state)
    {
        _ui.SetUiState(entity, CharacterRecordConsoleKey.Key, state);
    }

    private static bool IsSkippedRecord(StationRecordsFilter filter, FullCharacterRecords record)
    {
        if (StationRecordFilterHelper.IsFilterEmpty(filter, out var filterText))
            return false;

        return filter.Type switch
        {
            StationRecordFilterType.Name =>
                !StationRecordFilterHelper.ContainsText(record.Name, filterText),
            StationRecordFilterType.Job =>
                !StationRecordFilterHelper.ContainsText(record.JobTitle, filterText),
            StationRecordFilterType.Species =>
                !StationRecordFilterHelper.ContainsText(record.Species, filterText),
            StationRecordFilterType.Prints => record.Fingerprint != null
                && !StationRecordFilterHelper.MatchesCodePrefix(record.Fingerprint, filterText),
            StationRecordFilterType.DNA => record.DNA != null
                && !StationRecordFilterHelper.MatchesCodePrefix(record.DNA, filterText),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), "Invalid Character Record filter type"),
        };
    }
}
