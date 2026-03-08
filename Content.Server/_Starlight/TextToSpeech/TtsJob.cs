using System.Text.Json.Serialization;

namespace Content.Server._Starlight.TextToSpeech;

public sealed class TtsJob
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("t")]
    public required string Text { get; set; }

    [JsonPropertyName("r")]
    public required string Voice { get; set; }

    [JsonPropertyName("e")]
    public int Effect { get; set; }

    [JsonPropertyName("ts")]
    public long Timestamp { get; set; }
}

[JsonSerializable(typeof(TtsJob))]
internal sealed partial class TtsJobContext : JsonSerializerContext
{
}