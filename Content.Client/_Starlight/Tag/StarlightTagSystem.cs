using Content.Shared._Starlight.Tag;
using Content.Shared.Tag;
using Robust.Client.Player;

namespace Content.Client._Starlight.Tag;
public sealed class StarlightTagSystem : StarlightSharedTagSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TagComponent, AfterAutoHandleStateEvent>(OnTagChanged);
    }

    private void OnTagChanged(Entity<TagComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if(ent == _player.LocalEntity)
        {
            var ev = new InvalidateLocalEntityTagEvent();
            RaiseLocalEvent(ref ev);
        }
    }
}

[ByRefEvent]
public record struct InvalidateLocalEntityTagEvent()
{
}
