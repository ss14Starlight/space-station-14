// ReSharper disable CheckNamespace
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Content.Client._Starlight.UserInterface;
using Content.Shared.Medical.CrewMonitoring;

namespace Content.Client.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringWindow : PopOutFancyWindow
{
    public event Action<string>? FilterSelected;

    /// <summary>
    /// Populates the tracking filter with the allowed tracking implants and sets initial tracker.
    /// <summary>
    /// <param name="allowedTrackers"> The given list of trackers allowed in the monitor, organised into lists, where [0] is the prototype ID, and where [1] is the localisation label.</param>
    /// /// <param name="initialTracker"> The tracker filter to be selected by default, should be taken from ShownTrackingImplant in CrewMonitoringFilterComponent.</param>
    public void PopulateTrackingFilters(List<string[]> allowedTrackers, string? initialTracker)
    {
        if (allowedTrackers.Count == 0)
            return;

        int InitialIndex = 0;

        AddTrackerFilter("AllTrackers", "tracking-implant-filter-label-all");
        foreach (string[] TrackerType in allowedTrackers)
        {
            if (TrackerType[0] == initialTracker)
                InitialIndex = TrackingFilter.ItemCount;

            AddTrackerFilter(TrackerType[0], TrackerType[1]);
        }

        TrackingFilter.Visible = true;
        TrackingFilter.Select(InitialIndex);
    }

    private void AddTrackerFilter(string trackingImplant, string label)
    {
        TrackingFilter.AddItem(Loc.GetString(label));
        TrackingFilter.SetItemMetadata(TrackingFilter.ItemCount - 1, trackingImplant);
    }

    private void OnFilterSelected(OptionButton.ItemSelectedEventArgs eventArgs)
    {
        TrackingFilter.Select(eventArgs.Id);

        if (eventArgs.Button.SelectedMetadata is string TrackingImplant)
        {
            FilterSelected?.Invoke(TrackingImplant);
        }
    }

}
