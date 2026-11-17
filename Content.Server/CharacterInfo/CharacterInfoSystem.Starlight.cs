using Content.Server._Starlight.Objectives.ObjectivePicker;

namespace Content.Server.CharacterInfo;

public sealed partial class CharacterInfoSystem
{
    [Dependency] private AntagRandomObjectivesSystem _objectivePicker = default!;
    private string? GetCharacterBriefing(EntityUid mindId)
    {
        if (_roles.MindIsAntagonist(mindId) || !_jobs.MindTryGetJob(mindId, out var job))
            return _roles.MindGetBriefing(mindId);

        var briefing = Loc.GetString("job-greet-supervisors-warning",
            ("jobName", job.LocalizedName),
            ("supervisors", Loc.GetString(job.Supervisors)));

        if (job.JobRules is { } rules)
            briefing += "\n" + Loc.GetString("job-greet-information-rules", ("jobRules", Loc.GetString(rules)));

        return briefing;
    }
}
