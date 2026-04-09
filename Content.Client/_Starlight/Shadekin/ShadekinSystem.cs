using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        base.Initialize();

        InitializeBrighteye();
    }
}
