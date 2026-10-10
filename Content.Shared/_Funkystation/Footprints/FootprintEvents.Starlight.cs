namespace Content.Shared._Funkystation.Footprints;

/// <summary>
/// Which neighboring footprint tiles are cleaned along with the targeted footprint.
/// </summary>
public enum FootprintCleaningPattern : byte
{
    Target,
    Plus,
    Square,
}
