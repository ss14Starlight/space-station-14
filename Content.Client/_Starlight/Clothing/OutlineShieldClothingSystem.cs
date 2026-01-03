using Content.Shared._Starlight.Clothing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Clothing;

public sealed class OutlineShieldClothingSystem : SharedOutlineShieldClothingSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly Dictionary<EntityUid, ShaderInstance> _activeShaders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OutlineShieldClothingComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<OutlineShieldClothingComponent> ent, ref ComponentShutdown args)
    {
        // Clean up shader when component is removed
        if (ent.Comp.Wearer != null && _activeShaders.TryGetValue(ent.Comp.Wearer.Value, out var shader))
        {
            RemoveShaderFromWearer(ent.Comp.Wearer.Value, shader);
        }
    }

    public override void SetShieldActive(EntityUid uid, bool active, OutlineShieldClothingComponent? component = null)
    {
        base.SetShieldActive(uid, active, component);

        if (!Resolve(uid, ref component))
            return;

        if (component.Wearer == null)
            return;

        if (active)
        {
            ApplyShaderToWearer(component.Wearer.Value, component.ShaderPrototype);
        }
        else
        {
            if (_activeShaders.TryGetValue(component.Wearer.Value, out var shader))
            {
                RemoveShaderFromWearer(component.Wearer.Value, shader);
            }
        }
    }

    private void ApplyShaderToWearer(EntityUid wearer, string shaderProtoId)
    {
        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        // Create shader instance
        if (!_prototypeManager.TryIndex<ShaderPrototype>(shaderProtoId, out var prototype))
            return;

        var shader = prototype.InstanceUnique();
        sprite.PostShader = shader;

        // Track the shader so we can remove it later
        _activeShaders[wearer] = shader;
    }

    private void RemoveShaderFromWearer(EntityUid wearer, ShaderInstance shader)
    {
        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        // Only remove if it's still our shader
        if (sprite.PostShader == shader)
        {
            sprite.PostShader = null;
        }

        _activeShaders.Remove(wearer);
        shader.Dispose();
    }
}
