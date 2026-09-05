
using Robust.Shared.Random;
using Content.Shared.Roles.Jobs;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using System.Globalization;
using System.Linq;

namespace Content.Shared._Starlight.StoryGen;

/// <summary>
/// Extension to ContentLocalizationManager that adds more flavorful functionality needed for dynamic story generation.
/// New managers need to be registered in C.Client/Entry/EntryPoint.cs and C.Server/Entry/EntryPoint.cs, so just add stuff here instead.
/// </summary>
public sealed partial class StoryGenLocalizationManager
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEntitySystemManager _entSysMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;

    public const string SawmillId = "lazy.debugging";
    private ISawmill _sawmill = default!;

    public void Initialize(ILocalizationManager _loc, CultureInfo culture)
    {
        _loc.AddFunction(culture, "DATASET", FormatRandomDataset);
        _loc.AddFunction(culture, "JOB", FormatEntityJob);
        _loc.AddFunction(culture, "TO-UPPER", FuncToUpper);
    }

    /// <summary>
    /// {DATASET(protoId)}: In localization strings, given the name of a localizedDataset prototype, selects a value at random.
    /// </summary>
    private ILocValue FormatRandomDataset(LocArgs args)
    {
        var datasetName = args.Args[0].Format(new LocContext()).Replace("{", "").Replace("}", ""); // inelegant but adequate
        if (datasetName == null)
        {
            return new LocValueString("[color=darkred]Empty prototype name provided to DATASET()[/color]");
        }
        else
        {
            ProtoId<LocalizedDatasetPrototype> ds = datasetName!;
            try
            {
                var dataset = _protoMan.Index(ds);
                var pick = _random.Pick(dataset.Values);
                return new LocValueString(Loc.GetString(pick));
            }
            catch (Exception e)
            {
                var available_datasets = _protoMan.EnumeratePrototypes<LocalizedDatasetPrototype>().ToList().ToString();
                _sawmill.Debug("Available localized datasets: " + available_datasets);

                return new LocValueString("[color=darkred]Unknown prototype '" + datasetName + "' provided to DATASET()[/color]");
            }
        }
    }

    /// <summary>
    /// {TO-UPPER(string)}: In localization strings, returns the string passed in, fully capitalized.
    /// </summary>
    private ILocValue FuncToUpper(LocArgs args)
    {
        var input = args.Args[0].Format(new LocContext());
        if (!String.IsNullOrEmpty(input))
            return new LocValueString(input.ToUpper());
        else
            return new LocValueString("");
    }

    /// <summary>
    /// {JOB(entity)}: Given an entityUid in localization strings, returns its job; if no job, then its name;
    /// failing that, a generic term defined in locale files (e.g. 'crewmember').
    /// Mostly a wrapper for SharedJobSystem.MindTryGetJobName().
    /// </summary>
    private ILocValue FormatEntityJob(LocArgs args)
    {
        ILocValue entity0 = args.Args[0];
        if (entity0.Value is EntityUid entity)
        {
            var jobs = _entSysMan.GetEntitySystem<SharedJobSystem>();
            if (jobs.MindTryGetJobName(entity, out string jobName) && jobName != null) {
                return new LocValueString(jobName);
            } else {
                var entityName = _entMan.GetComponent<MetaDataComponent>(entity).EntityName;
                return new LocValueString(entityName);
            }
        }

        return new LocValueString(Loc.GetString("story-gen-jobless"));
    }
}
