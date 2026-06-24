using Content.Shared._Starlight.Shaders;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Shaders;

/// <summary>
/// Manages shader instances with automatic binding of additional textures
/// defined in <see cref="ShaderAdditionalParamsPrototype"/>.
/// </summary>
public sealed class StarlightShaderManager(IPrototypeManager protoMan, IResourceCache resourceCache) : IStarlightShaderManager
{
    private readonly Dictionary<ProtoId<ShaderPrototype>, ShaderInstance> _cache = [];

    /// <summary>
    /// Gets a cached unique shader instance with additional params (textures) bound.
    /// Returns null if the shader prototype doesn't exist.
    /// </summary>
    public ShaderInstance? GetShader(ProtoId<ShaderPrototype>? id)
    {
        if (id == null)
            return null;

        if (_cache.TryGetValue(id.Value, out var cached))
            return cached;

        if (!protoMan.TryIndex(id.Value, out var shaderProto))
            return null;

        var instance = shaderProto.InstanceUnique();

        // Bind additional textures if a matching ShaderAdditionalParams prototype exists
        if (protoMan.TryIndex<ShaderAdditionalParamsPrototype>(id.Value.Id, out var additional))
        {
            foreach (var (uniformName, texPath) in additional.Textures)
            {
                var texture = resourceCache.GetResource<TextureResource>(texPath).Texture;
                instance.SetParameter(uniformName, texture);
            }
        }

        _cache[id.Value] = instance;
        return instance;
    }
}
