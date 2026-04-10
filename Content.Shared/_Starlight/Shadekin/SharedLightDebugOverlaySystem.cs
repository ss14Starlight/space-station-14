using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shadekin;

public abstract class SharedLightDebugOverlaySystem : EntitySystem
{
    public const int LocalViewRange = 16;
    protected float AccumulatedFrameTime;

    [Serializable, NetSerializable]
    public sealed class LightDebugOverlayMessage : EntityEventArgs
    {
        public NetEntity GridId { get; }
        public Vector2i BaseIdx { get; }
        public byte[] OverlayData { get; }

        public LightDebugOverlayMessage(NetEntity gridId, Vector2i baseIdx, byte[] overlayData)
        {
            GridId = gridId;
            BaseIdx = baseIdx;
            OverlayData = overlayData;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LightDebugOverlayDisableMessage : EntityEventArgs
    {
    }
}
