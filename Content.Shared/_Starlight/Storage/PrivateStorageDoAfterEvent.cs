using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
// ReSharper disable CheckNamespace

namespace Content.Shared._Starlight.Storage;

[Serializable, NetSerializable]
public sealed partial class PrivateStorageDoAfterEvent : SimpleDoAfterEvent
{
    public readonly bool FromStrippingView;

    public PrivateStorageDoAfterEvent(bool fromStrippingView) => FromStrippingView = fromStrippingView;
}
