using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Shaders;

public interface IStarlightShaderManager
{
    ShaderInstance? GetShader(ProtoId<ShaderPrototype>? id);
}
