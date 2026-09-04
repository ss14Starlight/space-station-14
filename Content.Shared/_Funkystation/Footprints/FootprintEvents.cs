using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Footprints;

[ByRefEvent]
public record struct FootprintCleanEvent(bool Handled = false); // Moff - Track handling to play audio
