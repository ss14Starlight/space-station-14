using System.Text.Json.Serialization;

namespace Content.Server._Starlight.Discord;

public struct WebhookEmbedImage
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    public WebhookEmbedImage()
    {
    }
}
