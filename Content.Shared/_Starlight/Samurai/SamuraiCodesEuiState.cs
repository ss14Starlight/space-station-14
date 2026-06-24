using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Samurai;

[Serializable, NetSerializable]
public sealed class SamuraiCodesEuiState : EuiStateBase
{
    public List<SamuraiCode> Codes { get; }
    public NetEntity Target { get; }
    public SamuraiCodesEuiState(List<SamuraiCode> codes, NetEntity target)
    {
        Codes = codes;
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class SamuraiCodesSaveMessage : EuiMessageBase
{
    public List<SamuraiCode> Codes { get; }
    public NetEntity Target { get; }

    public SamuraiCodesSaveMessage(List<SamuraiCode> codes, NetEntity target)
    {
        Codes = codes;
        Target = target;
    }
}
