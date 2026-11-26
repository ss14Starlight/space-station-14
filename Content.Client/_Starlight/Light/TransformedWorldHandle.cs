using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Passes drawing on to another world handle with an extra transform applied after every transform that is set.
/// Lets code that draws in world space, such as sprite rendering, draw into a render target that expects its own
/// coordinates.
/// </summary>
public sealed class TransformedWorldHandle : DrawingHandleWorld
{
    private readonly DrawingHandleWorld _inner;
    private readonly Matrix3x2 _outer;
    private Matrix3x2 _local = Matrix3x2.Identity;

    public TransformedWorldHandle(DrawingHandleWorld inner, Matrix3x2 outer) : base(Texture.White)
    {
        _inner = inner;
        _outer = outer;
        _inner.SetTransform(_outer);
    }

    public override void SetTransform(in Matrix3x2 matrix)
    {
        _local = matrix;
        _inner.SetTransform(Matrix3x2.Multiply(matrix, _outer));
    }

    public override Matrix3x2 GetTransform() => _local;

    public override void UseShader(ShaderInstance? shader) => _inner.UseShader(shader);

    public override ShaderInstance? GetShader() => _inner.GetShader();

    public override void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
        ReadOnlySpan<DrawVertexUV2DColor> vertices)
        => _inner.DrawPrimitives(primitiveTopology, texture, vertices);

    public override void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
        ReadOnlySpan<ushort> indices,
        ReadOnlySpan<DrawVertexUV2DColor> vertices)
        => _inner.DrawPrimitives(primitiveTopology, texture, indices, vertices);

    public override void DrawCircle(Vector2 position, float radius, Color color, bool filled = true)
        => _inner.DrawCircle(position, radius, color, filled);

    public override void DrawLine(Vector2 from, Vector2 to, Color color) => _inner.DrawLine(from, to, color);

    public override void RenderInRenderTarget(IRenderTarget target, Action a, Color? clearColor)
        => _inner.RenderInRenderTarget(target, a, clearColor);

    public override void DrawTexture(Texture texture, Vector2 position, Color? modulate = null)
        => _inner.DrawTexture(texture, position, modulate);

    public override void DrawRect(Box2 rect, Color color, bool filled = true) => _inner.DrawRect(rect, color, filled);

    public override void DrawRects(ReadOnlySpan<WorldRect> rects) => _inner.DrawRects(rects);

    public override void DrawRectsUnmodulated(ReadOnlySpan<WorldRect> rects) => _inner.DrawRectsUnmodulated(rects);

    public override void DrawRect(in Box2Rotated rect, Color color, bool filled = true)
        => _inner.DrawRect(rect, color, filled);

    public override void DrawTextureRectRegion(Texture texture, Box2 quad,
        Color? modulate = null, UIBox2? subRegion = null)
        => _inner.DrawTextureRectRegion(texture, quad, modulate, subRegion);

    public override void DrawTextureRectRegion(Texture texture, in Box2Rotated quad,
        Color? modulate = null, UIBox2? subRegion = null)
        => _inner.DrawTextureRectRegion(texture, quad, modulate, subRegion);

    public override void DrawTextureRects(Texture texture, ReadOnlySpan<WorldTextureRect> rects)
        => _inner.DrawTextureRects(texture, rects);

    public override void DrawTextureRectsUnmodulated(Texture texture, ReadOnlySpan<WorldTextureRect> rects)
        => _inner.DrawTextureRectsUnmodulated(texture, rects);

    public override void RenderSpritePostShaders(
        Entity<SpriteComponent> sprite,
        IReadOnlyList<SpriteComponent.PostShaderEntry> postShaders,
        Angle eyeRotation,
        Angle worldRotation,
        Vector2 worldPosition,
        Direction? overrideDirection)
        => _inner.RenderSpritePostShaders(sprite, postShaders, eyeRotation, worldRotation, worldPosition, overrideDirection);
}
