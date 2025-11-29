using Content.Shared.Access.Systems;
using JetBrains.Annotations;
// Starlight Start
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
// Starlight End

namespace Content.Client.Access
{
    [UsedImplicitly]
    public sealed class IdCardConsoleSystem : SharedIdCardConsoleSystem
    {
        // one day, maybe bound user interfaces can be shared too.
        // then this doesn't have to be like this.
        // I hate this.

        // Starlight start
        [Dependency] private readonly SpriteSystem _spriteSystem = default!;
        
        public override void Initialize()
        {
            base.Initialize();
            
            SubscribeNetworkEvent<IdCardAccessUpdatedEvent>(OnAccessUpdated);
        }

        private void OnAccessUpdated(IdCardAccessUpdatedEvent ev)
        {
            var id = GetEntity(ev.TargetId);
            if (!TryComp<VisitorIdCardComponent>(id, out var comp)) return;
            if (!TryComp<SpriteComponent>(id, out var sprite)) return;
            if (comp.AccessSet) return; // already set
            _spriteSystem.LayerSetRsiState((id, sprite), 2, new RSI.StateId("outofsector"));
        }
        // Starlight end
    }
}
