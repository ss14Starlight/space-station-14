using Content.Shared.DoAfter;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Do-after raised when finishing screwdriver extraction of items from <see cref="ItemBorgModuleComponent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BorgModuleItemExtractionDoAfterEvent : SimpleDoAfterEvent;
