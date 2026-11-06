// ReSharper disable CheckNamespace
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Content.Shared.Preferences;
using Content.Shared.Spawners.Components;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class ContainerSpawnPointSystem
{
    private bool IsJobAllowed(ContainerSpawnPointComponent spawnPoint, ProtoId<JobPrototype>? job)
    {
        if (job == null)
            return false;

        if (spawnPoint.Job != null)
            return spawnPoint.Job == job;

        if (spawnPoint.Department != null &&
            _proto.Resolve(spawnPoint.Department.Value, out var departmentProto))
        {
            return departmentProto.Roles.Contains(job.Value);
        }
            return true;
    }

    private int GetSpawnPriority(ContainerSpawnPointComponent spawnPoint)
    {
        if (spawnPoint.Job != null)
            return 2;

        if (spawnPoint.Department != null)
            return 1;

        return 0;
    }
}
