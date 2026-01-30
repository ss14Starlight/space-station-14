using System;

namespace Content.Shared.StationRecords;

/// <summary>
/// Shared helper methods for station record filtering logic.
/// </summary>
public static class StationRecordFilterHelper
{
    /// <summary>
    /// Returns true if the filter is null or empty; otherwise exposes the normalized filter text.
    /// </summary>
    public static bool IsFilterEmpty(StationRecordsFilter? filter, out string filterText)
    {
        if (filter == null)
        {
            filterText = string.Empty;
            return true;
        }

        filterText = filter.Value?.Trim() ?? string.Empty;
        return filterText.Length == 0;
    }

    public static bool ContainsText(string? value, string filterText)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.Contains(filterText, StringComparison.CurrentCultureIgnoreCase);
    }

    public static bool MatchesCodePrefix(string? value, string filterText)
    {
        return !string.IsNullOrEmpty(value)
               && value.StartsWith(filterText, StringComparison.CurrentCultureIgnoreCase);
    }
}
