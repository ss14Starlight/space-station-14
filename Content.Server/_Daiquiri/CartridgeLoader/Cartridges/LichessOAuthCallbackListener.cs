using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// Short-lived loopback HTTP listener used to receive the Lichess OAuth redirect.
/// It binds to 127.0.0.1, accepts a single callback, and never stores tokens.
///
/// The server hosts this listener so that local clients can finish the flow without
/// doing any networking themselves (the client sandbox forbids raw sockets). Remote
/// clients can still paste the redirect URL into the PDA; in that case this listener
/// simply times out.
/// </summary>
public sealed class LichessOAuthCallbackListener : IDisposable
{
    private readonly TcpListener _listener;
    private readonly string _callbackPath;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public LichessOAuthCallbackListener(int port, string callbackPath)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _callbackPath = string.IsNullOrEmpty(callbackPath) ? "/" : callbackPath;
    }

    public void Start()
    {
        _listener.Start();
    }

    public async Task<LichessOAuthCallback?> WaitForCallbackAsync(TimeSpan timeout)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (true)
            {
                using var client = await _listener.AcceptTcpClientAsync(timeoutCts.Token);
                var callback = await HandleClientAsync(client, timeoutCts.Token);
                if (callback != null)
                    return callback;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<LichessOAuthCallback?> HandleClientAsync(
        TcpClient client,
        CancellationToken cancel)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var requestLine = await reader.ReadLineAsync(cancel);
        if (string.IsNullOrEmpty(requestLine))
            return null;

        var parts = requestLine.Split(' ');
        if (parts.Length < 2 ||
            !parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var target = parts[1];
        if (!Uri.TryCreate("http://127.0.0.1" + target, UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.AbsolutePath, _callbackPath, StringComparison.Ordinal))
        {
            await WriteResponseAsync(
                stream,
                404,
                "Not Found",
                "Неверный адрес обратного вызова Lichess.",
                cancel);
            return null;
        }

        var query = ParseQuery(uri.Query);
        query.TryGetValue("code", out var code);
        query.TryGetValue("state", out var state);
        query.TryGetValue("error", out var error);
        query.TryGetValue("error_description", out var errorDescription);

        if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(error))
        {
            await WriteResponseAsync(
                stream,
                400,
                "Bad Request",
                "Lichess не передал код авторизации.",
                cancel);
            return null;
        }

        var callback = new LichessOAuthCallback(
            code ?? string.Empty,
            state ?? string.Empty,
            error ?? string.Empty,
            errorDescription ?? string.Empty);

        var message = string.IsNullOrEmpty(error)
            ? "Ответ Lichess получен. Вернитесь в игру — Chess.NT покажет результат привязки."
            : $"Не удалось привязать аккаунт: {errorDescription ?? error}";

        await WriteResponseAsync(stream, 200, "OK", message, cancel);
        return callback;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (query.StartsWith('?'))
            query = query[1..];

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator >= 0 ? pair[..separator] : pair;
            var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;

            result[Uri.UnescapeDataString(key.Replace('+', ' '))] =
                Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return result;
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int status,
        string statusText,
        string message,
        CancellationToken cancel)
    {
        var html =
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>Chess.NT</title></head>" +
            "<body style=\"font-family:sans-serif;background:#3B2416;color:#F2D7A0;text-align:center;padding-top:80px\">" +
            "<h1>Chess.NT</h1><p>" + WebUtility.HtmlEncode(message) + "</p></body></html>";

        var body = Encoding.UTF8.GetBytes(html);
        var header =
            $"HTTP/1.1 {status} {statusText}\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancel);
        await stream.WriteAsync(body, cancel);
        await stream.FlushAsync(cancel);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}

public sealed record LichessOAuthCallback(
    string Code,
    string State,
    string Error,
    string ErrorDescription);
