using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Events;

[Serializable, NetSerializable]
public sealed partial class EnvironmentalScrapeDoAfterEvent : SimpleDoAfterEvent;
