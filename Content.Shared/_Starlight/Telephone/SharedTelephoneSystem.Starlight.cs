using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Shared.Telephone;

public abstract partial class SharedTelephoneSystem
{
    /// <summary>
    /// Checks whether <paramref name="source"/> can reach the receiver described by the supplied range, map, and grid values.
    /// This checks the direction from the source to the receiver.
    /// </summary>
    /// <param name="source">The transmitting telephone.</param>
    /// <param name="sourceMapId">The source telephone's map.</param>
    /// <param name="sourceGridUid">The source telephone's grid, if any.</param>
    /// <param name="receiverTransmissionRange">The receiver telephone's transmission range.</param>
    /// <param name="receiverMapId">The receiver telephone's map.</param>
    /// <param name="receiverGridUid">The receiver telephone's grid, if any.</param>
    public bool IsSourceInRangeOfReceiver(
        TelephoneComponent source,
        MapId sourceMapId,
        EntityUid? sourceGridUid,
        TelephoneRange receiverTransmissionRange,
        MapId receiverMapId,
        EntityUid? receiverGridUid)
    {
        if (!source.CompatibleRanges.Contains(receiverTransmissionRange))
            return false;

        if (source.IgnoreTelephonesOnSameGrid &&
            source.TransmissionRange != TelephoneRange.Grid &&
            receiverGridUid == sourceGridUid)
            return false;

        return source.TransmissionRange switch
        {
            TelephoneRange.Grid => sourceGridUid == receiverGridUid,
            TelephoneRange.Map => sourceMapId == receiverMapId,
            TelephoneRange.Unlimited => true,
            _ => false
        };
    }
}
