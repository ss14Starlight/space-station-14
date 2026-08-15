using Content.Shared.Localizations;
using Robust.Shared.Random;
using Content.Shared.Roles.Jobs;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.StoryGen;

public sealed partial class StoryGenLocalizationManager
{
    [Dependency] private ContentLocalizationManager _clm = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;

    public void Initialize()
    {
        var culture = _clm.GetActiveCulture();
        var _loc = _clm.GetLocalizationManager();

        if(culture != null) {
            _loc.AddFunction(culture, "DATASET", FormatRandomDataset);
            _loc.AddFunction(culture, "JOB", FormatEntityJob);

        } else
            throw new Exception("No active culture. Panic.");
    }

    /// <summary>
    /// Given the name of a localizedDataset prototype, selects a value at random.
    /// </summary>
    private ILocValue FormatRandomDataset(LocArgs args)
    {
        ILocValue datasetName = args.Args[0];
        if(datasetName.Value is ProtoId<LocalizedDatasetPrototype> ds)
        {
            var dataset = _protoMan.Index(ds);
            var pick = _random.Pick(dataset.Values);
            return new LocValueString(pick);
        } else if(datasetName.Value != null) {
            return new LocValueString("Unknown prototype: " + datasetName.Value.ToString());
        } else {
            return new LocValueString("Missing argument to DATASET()");
        }
    }

    /// <summary>
    /// Given an entity, returns its job; if no job, then its name. Wrapper for SharedJobSystem.MindTryGetJobName().
    /// </summary>
    private ILocValue FormatEntityJob(LocArgs args)
    {
        ILocValue entity0 = args.Args[0];
        if (entity0.Value is EntityUid entity)
        {
            if(_jobs.MindTryGetJobName(entity, out string jobName) && jobName != null) {
                return new LocValueString(jobName);
            } else {
                var entityName = _entMan.GetComponent<MetaDataComponent>(entity).EntityName;
                return new LocValueString(entityName);
            }
        }

        return new LocValueString(Loc.GetString("story-gen-jobless"));
    }
}
