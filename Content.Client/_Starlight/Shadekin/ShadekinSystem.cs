using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        InitializeBrighteye();
    }
}
