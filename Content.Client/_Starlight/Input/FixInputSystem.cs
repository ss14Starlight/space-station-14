using Content.Shared._Starlight.Input;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Input;

public sealed partial class FixInputSystem : EntitySystem
{
    [Dependency] private InputSystem _input = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FixInputEvent>(OnFixInput);
    }

    private void OnFixInput(FixInputEvent ev) => _input.SetEntityContextActive();
}
