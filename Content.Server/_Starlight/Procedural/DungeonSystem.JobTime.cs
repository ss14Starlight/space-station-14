using Content.Shared._Starlight.CCVar;
using Robust.Shared.CPUJob.JobQueues.Queues;

// ReSharper disable once CheckNamespace
namespace Content.Server.Procedural;

public sealed partial class DungeonSystem
{
    private void InitializeJobTime()
        => Subs.CVar(_configManager, StarlightCCVars.DungeonJobTime, value =>
        {
            DungeonJobTime = value;
            _dungeonJobQueue.Budget = value;
        }, true);

    private sealed class TimedJobQueue : JobQueue
    {
        public double Budget = 0.005;

        public override double MaxTime => Budget;
    }
}
