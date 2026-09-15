using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer.StarlightSurgery;

[Serializable, NetSerializable]
public sealed partial class TutorialStarlightSurgeryDoAfterEvent : SimpleDoAfterEvent
{
    public string Part = string.Empty;
    public string Surgery = string.Empty;
    public string Step = string.Empty;
}
