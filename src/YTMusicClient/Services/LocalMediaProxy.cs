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
            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(5),
            MaxConnectionsPerServer = 50,
            EnableMultipleHttp2Connections = true
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

    private (string UserAgent, string? Origin) GetClientIdentity(string targetUrl)
    {
        // Default to Oculus VR (unthrottled line-rate audio)
        string userAgent = "com.google.android.apps.youtube.vr.oculus/1.65.10 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip";
        string? origin = null;

        // Priority matching: check ANDROID_VR and ANDROID_MUSIC BEFORE generic ANDROID to prevent substring collision!
        if (targetUrl.Contains("c=ANDROID_VR", StringComparison.OrdinalIgnoreCase))
        {
            userAgent = "com.google.android.apps.youtube.vr.oculus/1.65.10 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip";
        }
        else if (targetUrl.Contains("c=ANDROID_MUSIC", StringComparison.OrdinalIgnoreCase))
        {
            userAgent = "com.google.android.apps.youtube.music/8.39.42 (Linux; U; Android 15; en_US; Pixel 9 Pro; Build/AP4A.250205.002) gzip";
        }
        else if (targetUrl.Contains("c=ANDROID", StringComparison.OrdinalIgnoreCase))
        {
            userAgent = "com.google.android.youtube/21.26.364 (Linux; U; Android 15; en_US; Pixel 9 Pro; Build/AP4A.250205.002; Cronet/132.0.6834.79) gzip";
        }
        else if (targetUrl.Contains("c=VISIONOS", StringComparison.OrdinalIgnoreCase))
        {
            userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";
        }
        else if (targetUrl.Contains("c=IOS", StringComparison.OrdinalIgnoreCase))
        {
            userAgent = "com.google.ios.youtube/21.26.4 (iPhone16,2; U; CPU iOS 18_3_2 like Mac OS X;)";
        }
        else if (targetUrl.Contains("c=WEB_REMIX", StringComparison.OrdinalIgnoreCase))
        {
            userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
            origin = "https://music.youtube.com";
        }
        else if (targetUrl.Contains("c=WEB", StringComparison.OrdinalIgnoreCase))
        {
            userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
            origin = "https://www.youtube.com";
        }

        return (userAgent, origin);
    }

    private void ApplyHeaders(HttpRequestMessage msg, string userAgent, string? origin, string range)
    {
        msg.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        msg.Headers.TryAddWithoutValidation("Range", range);
        if (!string.IsNullOrEmpty(origin))
        {
            msg.Headers.TryAddWithoutValidation("Origin", origin);
            msg.Headers.TryAddWithoutValidation("Referer", origin + "/");
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

            // Robust URL extraction: req.RawUrl preserves full query string including sparams, sig, lsig without truncation at '&'
            string? targetUrl = null;
            if (req.RawUrl != null && req.RawUrl.Contains("url="))
            {
                var idx = req.RawUrl.IndexOf("url=");
                var rawParam = req.RawUrl.Substring(idx + 4);
                if (rawParam.StartsWith("http%3A", StringComparison.OrdinalIgnoreCase) ||
                    rawParam.StartsWith("https%3A", StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = Uri.UnescapeDataString(rawParam);
                }
                else
                {
                    targetUrl = rawParam;
                }
            }

            if (string.IsNullOrEmpty(targetUrl))
            {
                targetUrl = req.QueryString["url"];
            }

            if (string.IsNullOrEmpty(targetUrl))
            {
                res.StatusCode = 400;
                res.Close();
                return;
            }

            // Double unescape guard if URL parameters still contain percent-encoded ampersands
            if (targetUrl.Contains("%26") && !targetUrl.Contains("&"))
            {
                targetUrl = Uri.UnescapeDataString(targetUrl);
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

            var (userAgent, origin) = GetClientIdentity(targetUrl);

            // Upstream probe: request first chunk up to Math.Min(startByte + ChunkSize - 1, requestedEndByte ?? long.MaxValue)
            long probeEnd = startByte + ChunkSize - 1;
            if (requestedEndByte.HasValue && requestedEndByte.Value < probeEnd)
            {
                probeEnd = requestedEndByte.Value;
            }

            // Probe with multi-attempt resilience (retry with alternate client UA if 403 Forbidden is encountered)
            HttpResponseMessage? probeResp = null;
            string activeUserAgent = userAgent;
            string? activeOrigin = origin;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                var probeReq = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                ApplyHeaders(probeReq, activeUserAgent, activeOrigin, $"bytes={startByte}-{probeEnd}");

                try
                {
                    var resp = await _httpClient.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead);
                    if (resp.IsSuccessStatusCode)
                    {
                        probeResp = resp;
                        break;
                    }

                    AppLogger.Log($"[LocalMediaProxy] Upstream probe attempt {attempt + 1} returned {resp.StatusCode}");
                    resp.Dispose();

                    // Fallback UA strategy on 403 Forbidden or edge node errors
                    if (attempt == 0)
                    {
                        activeUserAgent = activeUserAgent.Contains("vr.oculus")
                            ? "com.google.android.youtube/21.26.364 (Linux; U; Android 15; en_US; Pixel 9 Pro; Build/AP4A.250205.002; Cronet/132.0.6834.79) gzip"
                            : "com.google.android.apps.youtube.vr.oculus/1.65.10 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip";
                    }
                    else if (attempt == 1)
                    {
                        activeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
                        activeOrigin = "https://music.youtube.com";
                    }

                    await Task.Delay(150);
                }
                catch (Exception pEx)
                {
                    AppLogger.LogError($"[LocalMediaProxy] Upstream probe attempt {attempt + 1} threw exception", pEx);
                    await Task.Delay(150);
                }
            }

            if (probeResp == null || !probeResp.IsSuccessStatusCode)
            {
                AppLogger.LogError($"[LocalMediaProxy] All upstream probe attempts exhausted for {targetUrl}");
                res.StatusCode = (int)(probeResp?.StatusCode ?? HttpStatusCode.Forbidden);
                res.Close();
                return;
            }

            using (probeResp)
            {
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

                res.ContentType = contentType;
                res.Headers["Accept-Ranges"] = "bytes";

                if (string.IsNullOrEmpty(rangeHeader))
                {
                    res.StatusCode = (int)HttpStatusCode.OK;
                    if (totalBytes > 0)
                    {
                        res.ContentLength64 = totalBytes;
                    }
                }
                else
                {
                    res.StatusCode = (int)HttpStatusCode.PartialContent;
                    if (totalBytes > 0)
                    {
                        res.Headers["Content-Range"] = $"bytes {startByte}-{effectiveEndByte}/{totalBytes}";
                    }
                    if (bytesToServe > 0)
                    {
                        res.ContentLength64 = bytesToServe;
                    }
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
                    ApplyHeaders(chunkReq, activeUserAgent, activeOrigin, $"bytes={currByte}-{nextEnd}");

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
