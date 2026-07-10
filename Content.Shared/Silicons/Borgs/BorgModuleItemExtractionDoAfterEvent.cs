using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.Borgs;

/// <summary>
/// Do-after raised when finishing screwdriver extraction of items from <see cref="Components.ItemBorgModuleComponent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BorgModuleItemExtractionDoAfterEvent : SimpleDoAfterEvent;
