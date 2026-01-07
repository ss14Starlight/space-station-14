using Content.Shared._Starlight.UniversalSpawner;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.UniversalSpawner;

[UsedImplicitly]
public sealed class UniversalSpawnerBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private UniversalSpawnerWindow? _window;

    public UniversalSpawnerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<UniversalSpawnerWindow>();
        _window.Initialize(_prototypeManager);
        _window.OnEntriesChanged += entries => SendMessage(new UniversalSpawnerUpdateEntriesMessage(entries));

        _window.OnSettingsChanged += (maxSpawns, offset, deleteAfterSpawn, spawnChance, minSpawns, minRolls, maxRolls, triggerType, triggerTimeSeconds, triggerGameRule, proximityRange) => 
            SendMessage(new UniversalSpawnerUpdateSettingsMessage(
                maxSpawns,
                offset,
                deleteAfterSpawn,
                spawnChance,
                minSpawns,
                minRolls,
                maxRolls,
                triggerType,
                triggerTimeSeconds,
                triggerGameRule,
                proximityRange));

        _window.OnTriggerSpawn += () => SendMessage(new UniversalSpawnerTriggerMessage());

        _window.OnReset += () => SendMessage(new UniversalSpawnerResetMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not UniversalSpawnerBoundUserInterfaceState castState)
            return;

        _window?.UpdateState(castState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
