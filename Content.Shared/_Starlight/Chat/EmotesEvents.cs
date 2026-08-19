using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Chat;

/// <summary>
/// Sent by the client when requesting the server to bind a specific emote (selected via right-click in the emote
/// radial menu) to a new quick action on the player.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestBindEmoteMessage(ProtoId<EmotePrototype> protoId) : EntityEventArgs
{
    public readonly ProtoId<EmotePrototype> ProtoId = protoId;
}
