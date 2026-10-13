using Content.Shared.Verbs;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Examine
{
    public static class ExamineSystemMessages
    {
        [Serializable, NetSerializable]
        public sealed class RequestExamineInfoMessage : EntityEventArgs
        {
            public readonly NetEntity NetEntity;

            public readonly int Id;

            public readonly bool GetVerbs;

            public readonly NetEntity? Examiner; // Starlight: optional examiner used by remote control
            public RequestExamineInfoMessage(NetEntity netEntity, int id, bool getVerbs = false, NetEntity? examiner = null)
            {
                NetEntity = netEntity;
                Id = id;
                GetVerbs = getVerbs;
                Examiner = examiner; // Starlight
            }
        }

        [Serializable, NetSerializable]
        public sealed class ExamineInfoResponseMessage : EntityEventArgs
        {
            public readonly NetEntity EntityUid;
            public readonly int Id;
            public readonly FormattedMessage Message;

            public List<Verb>? Verbs;

            public readonly bool CenterAtCursor;
            public readonly bool OpenAtOldTooltip;

            public readonly bool KnowTarget;

            public ExamineInfoResponseMessage(NetEntity entityUid, int id, FormattedMessage message, List<Verb>? verbs=null,
                bool centerAtCursor=true, bool openAtOldTooltip=true, bool knowTarget = true)
            {
                EntityUid = entityUid;
                Id = id;
                Message = message;
                Verbs = verbs;
                CenterAtCursor = centerAtCursor;
                OpenAtOldTooltip = openAtOldTooltip;
                KnowTarget = knowTarget;
            }
        }
    }
}
