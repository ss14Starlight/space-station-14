// Sol
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared._Sol.CCVar;
using Content.Shared.MassMedia.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.MassMedia.Systems;

public sealed partial class NewsSystem
{
    private static readonly HttpClient PlaySolHttp = new();
    private static readonly JsonSerializerOptions PlaySolJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private string _playSolApiBase = string.Empty;
    private string _playSolDeploySecret = string.Empty;
    private string _playSolAuthPath = string.Empty;
    private string _playSolNewsPath = "/api/v1/news";
    private Color _playSolNewsEmbedColor = Color.LawnGreen;
    private bool _playSolNewsSendDuringRound = true;

    private void InitializePlaySol()
    {
        _cfg.OnValueChanged(SolCCVars.PlaySolApiBase, v => _playSolApiBase = v.Trim().TrimEnd('/'), true);
        _cfg.OnValueChanged(SolCCVars.PlaySolDeploySecret, v => _playSolDeploySecret = v, true);
        _cfg.OnValueChanged(SolCCVars.PlaySolAuthPath, v => _playSolAuthPath = string.IsNullOrWhiteSpace(v) ? string.Empty : NormalizePath(v), true);
        _cfg.OnValueChanged(SolCCVars.PlaySolNewsPath, v => _playSolNewsPath = NormalizePath(v), true);
        _cfg.OnValueChanged(SolCCVars.PlaySolNewsEmbedColor, value =>
        {
            _playSolNewsEmbedColor = Color.LawnGreen;
            if (Color.TryParse(value, out var color))
                _playSolNewsEmbedColor = color;
        }, true);
        _cfg.OnValueChanged(SolCCVars.PlaySolNewsSendDuringRound, v => _playSolNewsSendDuringRound = v, true);
    }

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (string.IsNullOrEmpty(path))
            return "/";
        return path.StartsWith('/') ? path : "/" + path;
    }

    private bool PlaySolEnabled =>
        !string.IsNullOrWhiteSpace(_playSolApiBase) &&
        !string.IsNullOrWhiteSpace(_playSolDeploySecret) &&
        !string.IsNullOrWhiteSpace(_playSolAuthPath);

    private void TrySendArticleToPlaySol(NewsArticle article)
    {
        if (!PlaySolEnabled)
            return;

        _ = SendArticleToPlaySolAsync(article);
    }

    private async void SendArticlesListToPlaySol(IOrderedEnumerable<NewsArticle> articles)
    {
        foreach (var article in articles)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await SendArticleToPlaySolAsync(article);
        }
    }

    private async Task SendArticleToPlaySolAsync(NewsArticle article)
    {
        if (!PlaySolEnabled)
            return;

        try
        {
            var token = await ExchangePlaySolTokenAsync();
            if (token is null)
                return;

            var payload = new PlaySolNewsPayload
            {
                Title = article.Title,
                Content = FormattedMessage.RemoveMarkupPermissive(article.Content),
                Author = article.Author ?? Loc.GetString("news-discord-unknown-author"),
                Server = _baseServer.ServerName,
                Round = _ticker.RoundId,
                ShareTime = article.ShareTime.ToString(@"hh\:mm\:ss"),
                EmbedColor = _playSolNewsEmbedColor.ToHex(),
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, _playSolApiBase + _playSolNewsPath);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(payload, options: PlaySolJson);

            using var resp = await PlaySolHttp.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Log.Error($"PlaySol news post failed ({(int)resp.StatusCode}): {body}");
                return;
            }

            Log.Info("Sent news article to PlaySol");
        }
        catch (Exception e)
        {
            Log.Error($"Error while sending PlaySol news article:\n{e}");
        }
    }

    private async Task<string?> ExchangePlaySolTokenAsync()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _playSolApiBase + _playSolAuthPath);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playSolDeploySecret);
        req.Content = JsonContent.Create(new { scope = new[] { "news" } }, options: PlaySolJson);

        using var resp = await PlaySolHttp.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            Log.Error($"PlaySol token exchange failed ({(int)resp.StatusCode})");
            return null;
        }

        var doc = await resp.Content.ReadFromJsonAsync<PlaySolTokenResponse>(PlaySolJson);
        return string.IsNullOrWhiteSpace(doc?.Token) ? null : doc.Token;
    }

    private sealed class PlaySolNewsPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public int Round { get; set; }
        public string ShareTime { get; set; } = string.Empty;
        public string? EmbedColor { get; set; }
    }

    private sealed class PlaySolTokenResponse
    {
        public string? Token { get; set; }
    }
}
