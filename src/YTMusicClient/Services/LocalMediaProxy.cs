using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using YTMusicClient.Helpers;

namespace YTMusicClient.Services;

public class LocalMediaProxy : IDisposable
{
    private HttpListener? _listener;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public LocalMediaProxy()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.youtube.com/");

        Start();
    }

    private void Start()
    {
        try
        {
            // Find an open port
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
            AppLogger.Log($"LocalMediaProxy started on {BaseUrl}");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to start LocalMediaProxy", ex);
        }
    }

    private async Task ListenLoop()
    {
        while (_isRunning && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch
            {
                if (!_isRunning) break;
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        try
        {
            var req = context.Request;
            var res = context.Response;

            if (req.Url == null || !req.Url.AbsolutePath.StartsWith("/stream"))
            {
                res.StatusCode = 404;
                res.Close();
                return;
            }

            var targetUrl = req.QueryString["url"];
            if (string.IsNullOrEmpty(targetUrl))
            {
                res.StatusCode = 400;
                res.Close();
                return;
            }

            // Forward request with matching headers
            var forwardReq = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            if (targetUrl.Contains("googlevideo.com", StringComparison.OrdinalIgnoreCase))
            {
                forwardReq.Headers.TryAddWithoutValidation("User-Agent", "com.google.android.apps.youtube.vr.oculus/1.65.10 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip");
            }
            else
            {
                forwardReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                forwardReq.Headers.Referrer = new Uri("https://www.youtube.com/");
                forwardReq.Headers.TryAddWithoutValidation("Origin", "https://www.youtube.com");
            }

            // Forward Range header if present
            var rangeHeader = req.Headers["Range"];
            if (!string.IsNullOrEmpty(rangeHeader))
            {
                forwardReq.Headers.TryAddWithoutValidation("Range", rangeHeader);
            }

            var forwardResp = await _httpClient.SendAsync(forwardReq, HttpCompletionOption.ResponseHeadersRead);

            res.StatusCode = (int)forwardResp.StatusCode;
            if (forwardResp.Content.Headers.ContentType != null)
            {
                res.ContentType = forwardResp.Content.Headers.ContentType.ToString();
            }

            if (forwardResp.Content.Headers.ContentLength.HasValue)
            {
                res.ContentLength64 = forwardResp.Content.Headers.ContentLength.Value;
            }

            if (forwardResp.Content.Headers.ContentRange != null)
            {
                res.Headers["Content-Range"] = forwardResp.Content.Headers.ContentRange.ToString();
            }

            res.Headers["Accept-Ranges"] = "bytes";

            using var upstreamStream = await forwardResp.Content.ReadAsStreamAsync();
            await upstreamStream.CopyToAsync(res.OutputStream);
            res.Close();
        }
        catch (Exception ex)
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch { }
            AppLogger.LogError("LocalMediaProxy error forwarding stream", ex);
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
