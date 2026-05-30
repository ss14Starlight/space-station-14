using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Starlight.TextToSpeech;

public sealed class TTSExamineSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TextToSpeechComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamine);
    }

    private void OnGetExamine(Entity<TextToSpeechComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName)
            return;

        if (ent.Comp.VoicePrototypeId is null)
            return;

        if (!_prototype.TryIndex<VoicePrototype>(ent.Comp.VoicePrototypeId, out var voice))
            return;

        var msg = new FormattedMessage();

        var voiceLoc = Loc.GetString(voice.Name);
        msg.AddMarkupOrThrow(Loc.GetString("tts-examine", ("ent", ent.Owner), ("voice", voiceLoc)));

        _examine.AddDetailedExamineVerb(args, ent, msg, Loc.GetString("tts-examinable-verb-text"), "/Textures/_Starlight/Interface/VerbIcons/voice.192dpi.png", Loc.GetString("tts-examinable-verb-message"));
    }
}
