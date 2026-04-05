using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Content.Shared._Starlight.NanoChat;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.NanoChat;

/// <summary>
/// Rich text markup handler for inline emote rendering in NanoChat messages.
/// Usage: [emote="emotename"] or [emote name="emotename"]
/// </summary>
public sealed class NanoChatEmoteMarkup : IMarkupTagHandler
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    private SpriteSystem? _spriteSystem;

    // Configurable emote rendering sizes
    private const float DefaultEmoteSize = 32f;
    private const float TopMargin = -8f; // Alignment adjustment

    public NanoChatEmoteMarkup()
    {
        IoCManager.InjectDependencies(this);
    }

    public string Name => "emote";

    public bool TryCreateControl(MarkupNode node, out Control control)
    {
        control = default!;

        // Extract emote ID from node value or "name" attribute
        if (!TryGetEmoteId(node, out var emoteId))
            return false;

        // Look up emote in cache
        var emote = NanoChatEmoteCache.AllEmotes.TryGetValue(emoteId, out var data) ? data : null;
        if (emote == null)
            return false;

        // Get texture from sprite specifier
        if (!TryLoadEmoteTexture(emote.Sprite, out var texture))
            return false;

        // Parse optional size attribute
        var emoteSize = DefaultEmoteSize;
        if (node.Attributes.TryGetValue("size", out var sizeParam) &&
            sizeParam.TryGetLong(out var customSizeLong) &&
            customSizeLong > 0 && customSizeLong <= 64)
        {
            emoteSize = (int)customSizeLong.Value;
        }

        // Create the texture control
        control = CreateEmoteControl(texture, emoteSize, emote.DisplayName);
        return true;
    }

    private bool TryGetEmoteId(MarkupNode node, out string emoteId)
    {
        emoteId = string.Empty;

        // Try to get from node value first
        if (node.Value.TryGetString(out var nodeValue))
        {
            emoteId = nodeValue!;
            return !string.IsNullOrWhiteSpace(emoteId);
        }

        // Fall back to "name" attribute
        if (node.Attributes.TryGetValue("name", out var nameParam) &&
            nameParam.TryGetString(out var nameValue))
        {
            emoteId = nameValue!;
            return !string.IsNullOrWhiteSpace(emoteId);
        }

        return false;
    }

    private bool TryLoadEmoteTexture(SpriteSpecifier specifier, out Texture texture)
    {
        texture = default!;
        _spriteSystem ??= _entitySystemManager.GetEntitySystem<SpriteSystem>();

        switch (specifier)
        {
            case SpriteSpecifier.Texture texSpecifier:
                if (!_resourceCache.TryGetResource<TextureResource>(texSpecifier.TexturePath, out var textureResource))
                    return false;
                texture = textureResource.Texture;
                return true;

            case SpriteSpecifier.Rsi rsiSpecifier:
                try
                {
                    var state = _spriteSystem.GetState(rsiSpecifier);
                    texture = state.Frame0;
                    return true;
                }
                catch
                {
                    return false;
                }

            default:
                return false;
        }
    }

    private Control CreateEmoteControl(Texture texture, float size, string tooltipText)
    {
        var sizeVector = new Vector2(size, size);

        return new TextureRect
        {
            Texture = texture,
            MinSize = sizeVector,
            MaxSize = sizeVector,
            Margin = new Thickness(0, TopMargin, 0, 0),
            ToolTip = $":{tooltipText}:",
        };
    }
}
