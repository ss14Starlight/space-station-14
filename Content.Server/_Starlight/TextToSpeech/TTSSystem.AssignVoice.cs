using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Starlight.TextToSpeech;

namespace Content.Server.Starlight.TTS;

public sealed partial class TTSSystem
{
    private int GetOrAssignVoice(EntityUid uid, TextToSpeechComponent? component = default, int? fallbackVoice = null)
    {
        fallbackVoice ??= DefaultVoice;
        if (component is null && !TryComp(uid, out component))
            return fallbackVoice.Value;

        if (component.VoicePrototypeId is string voiceId
            && _prototypeManager.TryIndex(voiceId, out VoicePrototype? proto))
            return proto.Voice;

        var isHumanoid = false;
        Sex? sex = null;

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoidAppearanceComponent)
            && humanoidAppearanceComponent?.Sex is Sex sex1)
        {
            isHumanoid = true;
            sex = sex1;
        }

        if (TryComp<MindContainerComponent>(uid, out var mindContainer)
            && mindContainer.HasMind
            && TryComp<MindComponent>(mindContainer.Mind, out var mind))
        {
            if (isHumanoid)
            {
                if (mind.Voice is string mindVoiceId && _prototypeManager.TryIndex(mindVoiceId, out VoicePrototype? mindVoice))
                {
                    component.VoicePrototypeId = mindVoiceId;
                    return mindVoice.Voice;
                }
            }
            else
            {
                if (mind.SiliconVoice is string mindVoiceId && _prototypeManager.TryIndex(mindVoiceId, out VoicePrototype? mindVoice))
                {
                    component.VoicePrototypeId = mindVoiceId;
                    return mindVoice.Voice;
                }
            }
        }

        if (!_prototypeManager.TryGetInstances<VoicePrototype>(out var voices))
            return fallbackVoice.Value;

        return isHumanoid
            ? AssignRandomVoice([.. voices.Where(x => !x.Value.Silicon
                && (x.Value.Sex == Sex.Unsexed || sex == Sex.Unsexed || x.Value.Sex == sex))])
            : AssignRandomVoice([.. voices.Where(x => x.Value.Silicon)]);

        int AssignRandomVoice(KeyValuePair<string, VoicePrototype>[] voicePrototypes)
        {
            if (voicePrototypes.Length == 0)
                return fallbackVoice.Value;

            var index = _rng.Next(voicePrototypes.Length);
            var prototype = voicePrototypes[index];
            component.VoicePrototypeId = prototype.Value.ID;
            return prototype.Value.Voice;
        }
    }
}
