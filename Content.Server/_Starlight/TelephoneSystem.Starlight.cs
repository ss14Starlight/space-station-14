using Content.Shared.Telephone;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Server.Telephone;

public sealed partial class TelephoneSystem
{
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
