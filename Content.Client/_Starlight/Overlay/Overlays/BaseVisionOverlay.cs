using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Content.Client._Starlight.Computers.RemoteControl;

namespace Content.Client._Starlight.Overlay.Overlays;

/*
Time to mini rant here. this NEEDs to be a abstract because if its not, then
the overlay system will think its trying to apply multiple of the same overlay
Hence, itll remove all of them until theres only one, even if all the instances
youve added are different based on their fields. So to get around this,
we define all the actual BEHAVIOUR here, but then just make it appear as a new
type by inheriting from this and implementing nothing. Thanks Robust Toolbox team.
*/
public abstract partial class BaseVisionOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    private readonly HashSet<EntityUid> _activeViewers = [];
    private readonly RemoteControlInterface _remoteControl;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private protected readonly ShaderInstance _shader;
    protected IEntityManager EntityManager => _entityManager;

    public BaseVisionOverlay(ShaderPrototype shader)
    {
        IoCManager.InjectDependencies(this);
        _remoteControl = _entityManager.System<RemoteControlInterface>();
        _shader = shader.InstanceUnique();
    }

    public void SetActiveViewers(IEnumerable<EntityUid> viewers)
    {
        _activeViewers.Clear();
        _activeViewers.UnionWith(viewers);
    }

    protected bool TryGetActiveViewer(in OverlayDrawArgs args, out EntityUid viewer)
        => _remoteControl.TryGetViewportEntity(args, out viewer) && _activeViewers.Contains(viewer);

    protected virtual ShaderInstance GetShader(EntityUid viewer)
        => _shader;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
        => TryGetActiveViewer(args, out _);

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || !TryGetActiveViewer(args, out var viewer))
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;

        var shader = GetShader(viewer);
        shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        worldHandle.UseShader(shader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}
