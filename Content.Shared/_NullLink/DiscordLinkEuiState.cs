using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._NullLink;

[NetSerializable, Serializable]
public sealed class DiscordLinkEuiState : EuiStateBase
{
    public string Url { get; set; } = "";
}
