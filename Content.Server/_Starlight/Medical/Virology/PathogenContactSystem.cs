using Content.Shared._Starlight.Medical.Virology;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Turns completed physical interactions into bacterial exposure in either direction.
/// </summary>
public sealed partial class PathogenContactSystem : EntitySystem
{
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenTransmissionSystem _transmission = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenContactEvent>(OnContact);
    }

    private void OnContact(PathogenContactEvent ev)
    {
        if (ev.First == ev.Second)
            return;

        TryTransmit(ev.First, ev.Second);
        TryTransmit(ev.Second, ev.First);
    }

    private bool TryTransmit(EntityUid source, EntityUid target)
    {
        if (!TryComp<PathogenInfectionComponent>(source, out var infections))
            return false;

        foreach (var infection in infections.Infections)
        {
            if (infection.Stage < 1 ||
                !_registry.TryGetStrain(infection.Pathogen, out var strain) ||
                strain.PathogenType != PathogenType.Bacteria)
            {
                continue;
            }

            if (_transmission.TryExpose(target, strain, strain.Transmissibility))
                return true;
        }

        return false;
    }
}
