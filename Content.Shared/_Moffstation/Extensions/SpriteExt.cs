using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Content.Shared._Moffstation.Extensions;

public static class SpriteExt
{
    extension(PrototypeLayerData layer)
    {
        public PrototypeLayerData With(
            string? shader = null,
            string? texturePath = null,
            string? rsiPath = null,
            string? state = null,
            Vector2? scale = null,
            Angle? rotation = null,
            Vector2? offset = null,
            bool? visible = null,
            Color? color = null,
            HashSet<string>? mapKeys = null,
            LayerRenderingStrategy? renderingStrategy = null,
            PrototypeCopyToShaderParameters? copyToShaderParameters = null,
            bool? cycle = null,
            bool? loop = null
        ) => new()
        {
            Shader = shader ?? layer.Shader,
            TexturePath = texturePath ?? layer.TexturePath,
            RsiPath = rsiPath ?? layer.RsiPath,
            State = state ?? layer.State,
            Scale = scale ?? layer.Scale,
            Rotation = rotation ?? layer.Rotation,
            Offset = offset ?? layer.Offset,
            Visible = visible ?? layer.Visible,
            Color = color ?? layer.Color,
            MapKeys = mapKeys ?? layer.MapKeys,
            RenderingStrategy = renderingStrategy ?? layer.RenderingStrategy,
            CopyToShaderParameters = copyToShaderParameters ?? layer.CopyToShaderParameters,
            Cycle = cycle ?? layer.Cycle,
            Loop = loop ?? layer.Loop,
        };

        /// Returns a copy of the receiver layer, with the given parameters added to the layer's existing values for
        /// those parameters. Parameters which are null do not modify anything. If the existing layer does not have
        /// values corresponding these parameters, the parameter's value is used.
        public PrototypeLayerData Plus(
            Vector2? scale = null,
            Angle? rotation = null,
            Vector2? offset = null
        ) => layer.With(
            scale: CombineNullably(layer.Scale, scale, (l, r) => l + r),
            rotation: CombineNullably(layer.Rotation, rotation, (l, r) => l + r),
            offset: CombineNullably(layer.Offset, offset, (l, r) => l + r)
        );

        public PrototypeLayerData WithUnlessAlreadySpecified(
            string? shader = null,
            string? texturePath = null,
            string? rsiPath = null,
            string? state = null,
            Vector2? scale = null,
            Angle? rotation = null,
            Vector2? offset = null,
            bool? visible = null,
            Color? color = null,
            HashSet<string>? mapKeys = null,
            LayerRenderingStrategy? renderingStrategy = null,
            PrototypeCopyToShaderParameters? copyToShaderParameters = null
        ) => new()
        {
            Shader = layer.Shader ?? shader,
            TexturePath = layer.TexturePath ?? texturePath,
            RsiPath = layer.RsiPath ?? rsiPath,
            State = layer.State ?? state,
            Scale = layer.Scale ?? scale,
            Rotation = layer.Rotation ?? rotation,
            Offset = layer.Offset ?? offset,
            Visible = layer.Visible ?? visible,
            Color = layer.Color ?? color,
            MapKeys = layer.MapKeys ?? mapKeys,
            RenderingStrategy = layer.RenderingStrategy ?? renderingStrategy,
            CopyToShaderParameters = layer.CopyToShaderParameters ?? copyToShaderParameters,
        };
    }

    extension(IEnumerable<PrototypeLayerData> layers)
    {
        public PrototypeLayerData[] With(
            string? shader = null,
            string? texturePath = null,
            string? rsiPath = null,
            string? state = null,
            Vector2? scale = null,
            Angle? rotation = null,
            Vector2? offset = null,
            bool? visible = null,
            Color? color = null,
            HashSet<string>? mapKeys = null,
            LayerRenderingStrategy? renderingStrategy = null,
            PrototypeCopyToShaderParameters? copyToShaderParameters = null,
            bool? cycle = null,
            bool? loop = null
        ) => layers.Select(l => l.With(
                    shader,
                    texturePath,
                    rsiPath,
                    state,
                    scale,
                    rotation,
                    offset,
                    visible,
                    color,
                    mapKeys,
                    renderingStrategy,
                    copyToShaderParameters,
                    cycle,
                    loop
                )
            )
            .ToArray();

        public PrototypeLayerData[] WithUnlessAlreadySpecified(
            string? shader = null,
            string? texturePath = null,
            string? rsiPath = null,
            string? state = null,
            Vector2? scale = null,
            Angle? rotation = null,
            Vector2? offset = null,
            bool? visible = null,
            Color? color = null,
            HashSet<string>? mapKeys = null,
            LayerRenderingStrategy? renderingStrategy = null,
            PrototypeCopyToShaderParameters? copyToShaderParameters = null
        ) => layers.Select(l => l.WithUnlessAlreadySpecified(
                    shader,
                    texturePath,
                    rsiPath,
                    state,
                    scale,
                    rotation,
                    offset,
                    visible,
                    color,
                    mapKeys,
                    renderingStrategy,
                    copyToShaderParameters
                )
            )
            .ToArray();
    }

    /// If <paramref name="l"/> and <paramref name="r"/> are null, returns null. If exactly one is not null, returns
    /// that value. If both are not null, combines them with <paramref name="combine"/> and returns that.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T? CombineNullably<T>(T? l, T? r, Func<T, T, T> combine) where T : struct =>
        l.HasValue && r.HasValue ? combine(l.Value, r.Value) : l ?? r;
}
