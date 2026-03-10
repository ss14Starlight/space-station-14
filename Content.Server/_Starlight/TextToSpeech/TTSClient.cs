using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Starlight.CCVar;
using Prometheus;
using Robust.Shared.Configuration;
using StackExchange.Redis;
using EnumeratorCancellation = System.Runtime.CompilerServices.EnumeratorCancellationAttribute;

namespace Content.Server._Starlight.TextToSpeech;

public sealed class TTSClient : ITTSClient
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private const string Queue = "tts_jobs";
    private const int TimeoutS = 5;

    private static readonly byte[] _endMarker = "__END__"u8.ToArray();

    private static readonly Histogram _timeToFirstChunk = Metrics.CreateHistogram(
        "tts_time_to_first_chunk_seconds",
        "Time from TTS request to receiving the first audio chunk",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(0, 0.125, 42),
        });

    private static readonly Histogram _totalRequestTime = Metrics.CreateHistogram(
        "tts_total_request_time_seconds",
        "Total time to complete a TTS request",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(0, 0.125, 42),
        });

    private static readonly Counter _cacheHits = Metrics.CreateCounter(
        "tts_cache_hits_total",
        "Number of TTS requests served from cache");

    private static readonly Counter _cacheMisses = Metrics.CreateCounter(
        "tts_cache_misses_total",
        "Number of TTS requests that required generation");

    private static readonly Counter _errors = Metrics.CreateCounter(
        "tts_errors_total",
        "Number of TTS generation errors",
        new CounterConfiguration
        {
            LabelNames = ["type"]
        });

    private ISawmill _sawmill = default!;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;
    private ISubscriber? _subscriber;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill(nameof(TTSClient));
        _cfg.OnValueChanged(StarlightCCVars.TTSConnectionString, Reconnect, true);
    }

    private void Reconnect(string connectionString)
    {
        try
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                _sawmill.Warning("Redis TTS connection string is null, skipping connection");
                return;
            }

            _redis?.Dispose();
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
            _subscriber = _redis.GetSubscriber();
            _sawmill.Info("Connected to Redis TTS service");
        }
        catch (Exception ex)
        {
            _errors.WithLabels("connection").Inc();
            _sawmill.Error("Failed to connect to Redis TTS service: {Exception}", ex);
        }
    }

    public async IAsyncEnumerable<byte[]> GenerateTTS
    (
        string text,
        int voice,
        TTSEffect effect = TTSEffect.None,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (await GetCache(text, voice, effect) is byte[] cached)
        {
            _cacheHits.Inc();

            var offset = 0;
            while (offset + 4 <= cached.Length)
            {
                var length = BitConverter.ToUInt32(cached, offset);
                offset += 4;

                if (offset + length > cached.Length)
                    break;

                var chunk = new byte[length];
                Buffer.BlockCopy(cached, offset, chunk, 0, (int)length);
                offset += (int)length;

                yield return chunk;
            }

            yield return [];
            yield break;
        }

        _cacheMisses.Inc();
        await foreach (var chunk in GenerateStreamAsync(text, voice, effect, cancellationToken))
            yield return chunk;
    }

    private async Task<byte[]?> GetCache(string text, int voice, TTSEffect effect)
    {
        if (_db is null)
            return null;

        var cacheKey = effect != TTSEffect.None
            ? $"cache:{voice}:{(int)effect}:{text}"
            : $"cache:{voice}:{text}";

        var cached = await _db.StringGetAsync(cacheKey);
        return cached.HasValue ? (byte[])cached! : null;
    }

    private async IAsyncEnumerable<byte[]> GenerateStreamAsync
    (
        string text,
        int voice,
        TTSEffect effect = TTSEffect.None,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (_subscriber is null || _db is null)
        {
            _sawmill.Warning("TTS request skipped: Redis not connected");
            yield break;
        }

        var jobId = Guid.NewGuid().ToString("N");
        var channel = effect != TTSEffect.None ? $"result:{jobId}:{(int)effect}" : $"result:{jobId}";

        var pending = new byte[]?[256];
        byte nextSeq = 0;
        var completed = false;

        await _subscriber.SubscribeAsync(RedisChannel.Literal(channel), (_, message) =>
        {
            var data = (byte[])message!;
            if (data.SequenceEqual(_endMarker))
                completed = true;
            else if (data.Length > 1)
            {
                var seq = data[0];
                pending[seq] = data[1..];
            }
        });

        var stopwatch = Stopwatch.StartNew();
        var firstChunkReceived = false;

        try
        {
            var job = new TtsJob
            {
                Id = jobId,
                Text = text,
                Voice = voice + "",
                Effect = (int)effect,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var jobJson = JsonSerializer.Serialize(job, TtsJobContext.Default.TtsJob);

            await _db.ListLeftPushAsync(Queue, jobJson);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutS));

            while (!completed || pending[nextSeq] != null)
            {
                var chunk = pending[nextSeq];

                if (chunk != null)
                {
                    pending[nextSeq] = null;
                    nextSeq++;

                    if (!firstChunkReceived)
                    {
                        _timeToFirstChunk.Observe(stopwatch.Elapsed.TotalSeconds);
                        firstChunkReceived = true;
                    }
                    yield return chunk;
                    continue;
                }

                if (cts.Token.IsCancellationRequested)
                {
                    _errors.WithLabels("timeout").Inc();
                    _totalRequestTime.Observe(stopwatch.Elapsed.TotalSeconds);
                    _sawmill.Warning("TTS request timed out after {Timeout}s for job {JobId}", TimeoutS, jobId);
                    yield break;
                }

                await Task.Delay(10, cts.Token);
            }

            _totalRequestTime.Observe(stopwatch.Elapsed.TotalSeconds);
            yield return [];
        }
        finally
        {
            await _subscriber.UnsubscribeAsync(RedisChannel.Literal(channel));
        }
    }
}
