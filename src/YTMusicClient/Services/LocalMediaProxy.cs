using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using YTMusicClient.Helpers;

namespace YTMusicClient.Services;

/// <summary>
/// High-performance chunked local streaming proxy.
/// Bridges Windows MediaPlayer with Google Video CDN by sending exact matching
/// Client User-Agent and bounded byte-range headers (1MB chunks) required by googlevideo.com.
/// Eliminates HTTP 403 Forbidden and 0xC00D2EE7 errors.
/// </summary>
public class LocalMediaProxy : IDisposable
{
    private HttpListener? _listener;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private const int ChunkSize = 1024 * 1024; // 1 MB bounded chunk for unthrottled streaming

    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public LocalMediaProxy()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 20
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        Start();
    }

    private void Start()
    {
        try
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            Port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _isRunning = true;

            Task.Run(ListenLoop);
            AppLogger.Log($"[LocalMediaProxy] Started successfully on {BaseUrl}");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[LocalMediaProxy] Failed to start", ex);
        }
    }

    private async Task ListenLoop()
    {
        while (_isRunning && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context));
            }
            catch
            {
                if (!_isRunning) break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        try
        {
            if (req.Url == null || !req.Url.AbsolutePath.StartsWith("/stream"))
            {
                res.StatusCode = 404;
                res.Close();
                return;
            }

            var targetUrl = req.QueryString["url"];
            if (string.IsNullOrEmpty(targetUrl) && req.RawUrl != null && req.RawUrl.Contains("url="))
            {
                var idx = req.RawUrl.IndexOf("url=");
                targetUrl = Uri.UnescapeDataString(req.RawUrl.Substring(idx + 4));
            }

            if (string.IsNullOrEmpty(targetUrl))
            {
                res.StatusCode = 400;
                res.Close();
                return;
            }

            // Parse client requested byte range (e.g. bytes=0-, bytes=0-32767, or bytes=1048576-)
            long startByte = 0;
            long? requestedEndByte = null;
            var rangeHeader = req.Headers["Range"];
            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
            {
                var parts = rangeHeader.Substring(6).Split('-');
                if (long.TryParse(parts[0], out var parsedStart))
                {
                    startByte = parsedStart;
                }
                if (parts.Length > 1 && long.TryParse(parts[1], out var parsedEnd))
                {
                    requestedEndByte = parsedEnd;
                }
            }

            // Determine client identity matching for googlevideo URLs
            string userAgent = "com.google.android.apps.youtube.vr.oculus/1.65.10 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip";
            if (targetUrl.Contains("c=IOS", StringComparison.OrdinalIgnoreCase))
            {
                userAgent = "com.google.ios.youtube/21.26.4 (iPhone16,2; U; CPU iOS 18_3_2 like Mac OS X;)";
            }

            // Upstream probe: request first chunk up to Math.Min(startByte + ChunkSize - 1, requestedEndByte ?? long.MaxValue)
            long probeEnd = startByte + ChunkSize - 1;
            if (requestedEndByte.HasValue && requestedEndByte.Value < probeEnd)
            {
                probeEnd = requestedEndByte.Value;
            }

            using var probeReq = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            probeReq.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            probeReq.Headers.TryAddWithoutValidation("Range", $"bytes={startByte}-{probeEnd}");

            using var probeResp = await _httpClient.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead);
            if (!probeResp.IsSuccessStatusCode)
            {
                AppLogger.LogError($"[LocalMediaProxy] Upstream probe failed with HTTP {probeResp.StatusCode} for {targetUrl}");
                res.StatusCode = (int)probeResp.StatusCode;
                res.Close();
                return;
            }

            long totalBytes = -1;
            if (probeResp.Content.Headers.ContentRange != null && probeResp.Content.Headers.ContentRange.Length.HasValue)
            {
                totalBytes = probeResp.Content.Headers.ContentRange.Length.Value;
            }

            long effectiveEndByte = requestedEndByte.HasValue
                ? (totalBytes > 0 ? Math.Min(requestedEndByte.Value, totalBytes - 1) : requestedEndByte.Value)
                : (totalBytes > 0 ? totalBytes - 1 : probeEnd);

            long bytesToServe = effectiveEndByte - startByte + 1;

            string contentType = probeResp.Content.Headers.ContentType?.ToString() ?? "audio/mp4";

            res.StatusCode = (int)HttpStatusCode.PartialContent;
            res.ContentType = contentType;
            res.Headers["Accept-Ranges"] = "bytes";

            if (totalBytes > 0)
            {
                res.Headers["Content-Range"] = $"bytes {startByte}-{effectiveEndByte}/{totalBytes}";
            }
            if (bytesToServe > 0)
            {
                res.ContentLength64 = bytesToServe;
            }

            if (req.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            {
                res.Close();
                return;
            }

            // Pipe first chunk to client
            await probeResp.Content.CopyToAsync(res.OutputStream);
            await res.OutputStream.FlushAsync();

            if (probeEnd >= effectiveEndByte)
            {
                res.Close();
                return;
            }

            // Sequentially stream remaining 1MB bounded chunks to keep line-rate unthrottled streaming
            long currByte = probeEnd + 1;
            while (currByte <= effectiveEndByte && _isRunning)
            {
                long nextEnd = Math.Min(currByte + ChunkSize - 1, effectiveEndByte);
                using var chunkReq = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                chunkReq.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                chunkReq.Headers.TryAddWithoutValidation("Range", $"bytes={currByte}-{nextEnd}");

                using var chunkResp = await _httpClient.SendAsync(chunkReq, HttpCompletionOption.ResponseHeadersRead);
                if (!chunkResp.IsSuccessStatusCode)
                {
                    break;
                }

                await chunkResp.Content.CopyToAsync(res.OutputStream);
                await res.OutputStream.FlushAsync();

                currByte = nextEnd + 1;
            }

            res.Close();
        }
        catch (Exception)
        {
            // Normal when client pauses, seeks, or changes track
            try { res.Close(); } catch { }
        }
    }

    public string GetProxiedStreamUrl(string originalUrl)
    {
        return $"{BaseUrl}/stream?url={Uri.EscapeDataString(originalUrl)}";
    }

    public void Dispose()
    {
        _isRunning = false;
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        _httpClient.Dispose();
    }
}
