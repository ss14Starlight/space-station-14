using Content.Shared._Starlight.Input;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Input;

public sealed class FixInputSystem : EntitySystem
{
    [Dependency] private readonly InputSystem _input = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FixInputEvent>(OnFixInput);
    }

    private void OnFixInput(FixInputEvent ev) => _input.SetEntityContextActive();
}
