namespace Content.Shared._Funkystation.Footprints;

public sealed partial class FootprintCleanEvent : EntityEventArgs
{
    public FootprintCleaningPattern CleaningPattern { get; }

    public FootprintCleanEvent(FootprintCleaningPattern cleaningPattern) => CleaningPattern = cleaningPattern;
}

/// <summary>
/// Which neighboring footprint tiles are cleaned along with the targeted footprint.
/// </summary>
public enum FootprintCleaningPattern : byte
{
    Target,
    Plus,
    Square,
}
