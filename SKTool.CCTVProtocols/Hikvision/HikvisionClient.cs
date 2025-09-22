using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace SKTool.CCTVProtocols.Hikvision;

public sealed class HikvisionClient : IDisposable
{
    private readonly HikvisionDevice _device;
    private readonly HttpClient _http;
    private readonly SocketsHttpHandler _baseHandler;
    private readonly bool _preferDigest;
    private readonly ILogger _logger;

    private const int MaxRetries = 3;

    public HikvisionClient(
        HikvisionDevice device,
        bool preferDigest = true,
        TimeSpan? timeout = null,
        bool allowSelfSigned = true,
        TimeSpan? connectTimeout = null,
        ILogger? logger = null)
    {
        _device = device;
        _preferDigest = preferDigest;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HikvisionClient>.Instance;

        _baseHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            ConnectTimeout = connectTimeout ?? HikvisionConstants.DefaultTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = { RemoteCertificateValidationCallback = (s, cert, chain, errors) => allowSelfSigned || errors == System.Net.Security.SslPolicyErrors.None }
        };

        _http = new HttpClient(_baseHandler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(20)
        };
    }

    public void Dispose() => _http.Dispose();

    // High-level helpers
    public Task<XDocument> GetDeviceInfoAsync(CancellationToken ct = default)
        => GetXmlAsync(HikvisionConstants.SystemDeviceInfo, ct);

    public Task<XDocument> GetTimeAsync(CancellationToken ct = default)
        => GetXmlAsync(HikvisionConstants.SystemTime, ct);

    public Task<XDocument> GetTimeCapabilitiesAsync(CancellationToken ct = default)
        => GetXmlAsync(HikvisionConstants.SystemTimeCapabilities, ct);

    public Task<XDocument> SetTimeAsync(XDocument timeXml, CancellationToken ct = default)
        => PutXmlAsync(HikvisionConstants.SystemTime, timeXml, ct);

    public Task<XDocument> GetNtpServersAsync(CancellationToken ct = default)
        => GetXmlAsync(HikvisionConstants.SystemTimeNtpServers, ct);

    public Task<XDocument> SetNtpServersAsync(XDocument ntpXml, CancellationToken ct = default)
        => PutXmlAsync(HikvisionConstants.SystemTimeNtpServers, ntpXml, ct);

    public Task<XDocument> RebootAsync(CancellationToken ct = default)
    {
        var xml = XDocument.Parse(HikvisionXmlTemplates.RebootTemplate);
        return PutXmlAsync(HikvisionConstants.SystemReboot, xml, ct);
    }

    public Task<XDocument> GetNetworkInterfaceAsync(int id, CancellationToken ct = default)
        => GetXmlAsync(HikvisionConstants.NetworkInterface(id), ct);

    // Attempt IPAddress endpoints with fallback to full NetworkInterface PUT if needed
    public async Task<XDocument> SetNetworkInterfaceIpAddressAsync(int id, XDocument ipAddressXml, CancellationToken ct = default)
    {
        try
        {
            using var scope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object?>
            {
                ["device"] = _device.Url.Host,
                ["ifaceId"] = id,
                ["op"] = "SetIpAddress"
            });
            _logger.LogInformation("Setting IP address via lowercase endpoint for interface {Id}", id);
            return await PutXmlAsync(HikvisionConstants.NetworkInterfaceIpAddressLower(id), ipAddressXml, ct).ConfigureAwait(false);
        }
        catch (HikvisionIsapiException ex) when (IsRetryWorthy(ex))
        {
            _logger.LogWarning(ex, "Lowercase endpoint failed, trying uppercase for interface {Id}", id);
            try
            {
                return await PutXmlAsync(HikvisionConstants.NetworkInterfaceIpAddressUpper(id), ipAddressXml, ct).ConfigureAwait(false);
            }
            catch (HikvisionIsapiException ex2) when (IsRetryWorthy(ex2))
            {
                _logger.LogWarning(ex2, "Uppercase endpoint failed, falling back to full NetworkInterface PUT for interface {Id}", id);

                var current = await GetNetworkInterfaceAsync(id, ct).ConfigureAwait(false);
                var ns = current.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var root = current.Root ?? new XElement(ns + "NetworkInterface");

                // Ensure id present/matches
                HikvisionXml.SetOrAdd(root, ns + "id", id.ToString());

                // Replace IPAddress node with provided (preserving namespace)
                root.Element(ns + "IPAddress")?.Remove();
                root.Add(new XElement(ipAddressXml.Root!)); // Root is already namespaced

                var wrapped = new XDocument(new XElement(root)); // ensure a clean document
                return await PutXmlAsync(HikvisionConstants.NetworkInterface(id), wrapped, ct).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRetryWorthy(HikvisionIsapiException ex)
    {
        // 400 invalid content, 404 not found, 405 method not allowed, 415 unsupported media type, 501 not implemented
        return ex.HttpStatus == 400 || ex.HttpStatus == 404 || ex.HttpStatus == 405 || ex.HttpStatus == 415 || ex.HttpStatus == 501;
    }

    public Task<XDocument> GetStreamingChannelsAsync(CancellationToken ct = default)
        => GetXmlAsync(HikvisionConstants.StreamingChannels, ct);

    public Task<XDocument> GetChannelXmlAsync(int channelId, CancellationToken ct = default)
        => GetXmlAsync(HikvisionConstants.StreamingChannel(channelId), ct);

    public Task<XDocument> SetChannelXmlAsync(int channelId, XDocument xml, CancellationToken ct = default)
        => PutXmlAsync(HikvisionConstants.StreamingChannel(channelId), xml, ct);

    public Task<byte[]> GetSnapshotAsync(int channelId, CancellationToken ct = default)
        => GetBytesAsync(HikvisionConstants.StreamingChannelPicture(channelId), HikvisionConstants.JpegMediaType, ct);

    // Core verbs (XML)
    public Task<XDocument> GetXmlAsync(string path, CancellationToken ct = default)
        => SendXmlAsync(HttpMethod.Get, path, null, ct);

    public Task<XDocument> PutXmlAsync(string path, XDocument xml, CancellationToken ct = default)
        => SendXmlAsync(HttpMethod.Put, path, xml, ct);

    // Core verb (bytes)
    public Task<byte[]> GetBytesAsync(string path, string accept, CancellationToken ct = default)
        => SendBytesAsync(HttpMethod.Get, path, accept, ct);

    // Internals
    private async Task<XDocument> SendXmlAsync(HttpMethod method, string path, XDocument? body, CancellationToken ct)
    {
        var (status, reason, content) = await SendAsync(method, path, body, HikvisionConstants.XmlMediaType, ct).ConfigureAwait(false);

        if ((int)status < 200 || (int)status >= 300)
            ThrowIsapiException(status, reason, content);

        if (string.IsNullOrWhiteSpace(content))
            return XDocument.Parse("<Response/>");

        return XDocument.Parse(content);
    }

    private async Task<byte[]> SendBytesAsync(HttpMethod method, string path, string? accept, CancellationToken ct)
    {
        var uri = _device.Url.Build(path);
        var corr = Guid.NewGuid().ToString("N");

        using var scope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["device"] = _device.Url.Host,
            ["path"] = uri.PathAndQuery,
            ["corr"] = corr
        });

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var req = new HttpRequestMessage(method, uri);
                if (!_preferDigest)
                {
                    var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_device.Username}:{_device.Password}"));
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
                }
                if (!string.IsNullOrEmpty(accept))
                    req.Headers.Accept.ParseAdd(accept);

                _logger.LogDebug("Request attempt {Attempt}: {Method} {Uri} Accept={Accept}", attempt, method, uri, accept);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Unauthorized && _preferDigest)
                {
                    var challenge = GetDigestChallengeHeader(resp.Headers.WwwAuthenticate);
                    if (challenge is not null)
                    {
                        _logger.LogDebug("401 received. Retrying with Digest authentication for {Method} {Path}", method, uri.PathAndQuery);
                        using var retry = new HttpRequestMessage(method, uri);
                        if (!string.IsNullOrEmpty(accept))
                            retry.Headers.Accept.ParseAdd(accept);
                        retry.Headers.Authorization = BuildDigestAuthHeader(challenge, method.Method, uri.PathAndQuery);
                        using var retryResp = await _http.SendAsync(retry, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
                        sw.Stop();
                        _logger.LogInformation("Response: {Method} {Path} => {Status} in {Elapsed} ms (digest)", method, uri.PathAndQuery, (int)retryResp.StatusCode, sw.ElapsedMilliseconds);
                        if (!retryResp.IsSuccessStatusCode)
                        {
                            var err = await retryResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (IsTransientStatus((int)retryResp.StatusCode) && attempt < MaxRetries)
                            {
                                await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                                continue;
                            }
                            ThrowIsapiException(retryResp.StatusCode, retryResp.ReasonPhrase ?? "HTTP error", err);
                        }
                        return await retryResp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                sw.Stop();
                _logger.LogInformation("Response: {Method} {Path} => {Status} in {Elapsed} ms, {Bytes} bytes", method, uri.PathAndQuery, (int)resp.StatusCode, sw.ElapsedMilliseconds, bytes?.Length ?? 0);

                if (!resp.IsSuccessStatusCode)
                {
                    var err = Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>());
                    if (IsTransientStatus((int)resp.StatusCode) && attempt < MaxRetries)
                    {
                        _logger.LogWarning("Transient failure {Status}. Retrying attempt {Attempt}...", (int)resp.StatusCode, attempt + 1);
                        await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                        continue;
                    }
                    ThrowIsapiException(resp.StatusCode, resp.ReasonPhrase ?? "HTTP error", err);
                }

                return bytes!;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < MaxRetries)
            {
                _logger.LogWarning("Request timeout on attempt {Attempt}. Retrying...", attempt);
                await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                continue;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Transient network error on attempt {Attempt}. Retrying...", attempt);
                await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                continue;
            }
        }

        throw new HikvisionIsapiException(0, "Exhausted retries", string.Empty);
    }

    private async Task<(HttpStatusCode status, string reason, string content)> SendAsync(HttpMethod method, string path, XDocument? body, string? accept, CancellationToken ct)
    {
        var uri = _device.Url.Build(path);
        var corr = Guid.NewGuid().ToString("N");

        using var scope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["device"] = _device.Url.Host,
            ["path"] = uri.PathAndQuery,
            ["corr"] = corr
        });

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var req = new HttpRequestMessage(method, uri);

                if (!_preferDigest)
                {
                    var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_device.Username}:{_device.Password}"));
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
                }
                if (!string.IsNullOrEmpty(accept))
                    req.Headers.Accept.ParseAdd(accept);

                if (body != null)
                {
                    var xmlStr = body.Declaration is null
                        ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + body.ToString(SaveOptions.DisableFormatting)
                        : body.ToString(SaveOptions.DisableFormatting);

                    req.Content = new StringContent(xmlStr, Encoding.UTF8, HikvisionConstants.XmlMediaType);
                }

                _logger.LogDebug("Request attempt {Attempt}: {Method} {Uri} Accept={Accept} ContentLength={Len}",
                    attempt, method, uri, accept, req.Content?.Headers.ContentLength ?? 0);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Unauthorized && _preferDigest)
                {
                    var challenge = GetDigestChallengeHeader(resp.Headers.WwwAuthenticate);
                    if (challenge is not null)
                    {
                        _logger.LogDebug("401 received. Retrying with Digest authentication for {Method} {Path}", method, uri.PathAndQuery);

                        using var retry = new HttpRequestMessage(method, uri);
                        if (body != null)
                        {
                            var xmlStr = body.Declaration is null
                                ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + body.ToString(SaveOptions.DisableFormatting)
                                : body.ToString(SaveOptions.DisableFormatting);
                            retry.Content = new StringContent(xmlStr, Encoding.UTF8, HikvisionConstants.XmlMediaType);
                        }
                        if (!string.IsNullOrEmpty(accept))
                            retry.Headers.Accept.ParseAdd(accept);
                        retry.Headers.Authorization = BuildDigestAuthHeader(challenge, method.Method, uri.PathAndQuery);
                        using var retryResp = await _http.SendAsync(retry, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
                        var retryText = await retryResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        sw.Stop();
                        _logger.LogInformation("Response: {Method} {Path} => {Status} in {Elapsed} ms (digest), body length={Len}", method, uri.PathAndQuery, (int)retryResp.StatusCode, sw.ElapsedMilliseconds, retryText?.Length ?? 0);

                        if (!retryResp.IsSuccessStatusCode && IsTransientStatus((int)retryResp.StatusCode) && attempt < MaxRetries)
                        {
                            _logger.LogWarning("Transient status {Status}. Retrying attempt {Attempt}...", (int)retryResp.StatusCode, attempt + 1);
                            await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                            continue;
                        }

                        return (retryResp.StatusCode, retryResp.ReasonPhrase ?? "HTTP error", retryText);
                    }
                }

                var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                sw.Stop();
                _logger.LogInformation("Response: {Method} {Path} => {Status} in {Elapsed} ms, body length={Len}", method, uri.PathAndQuery, (int)resp.StatusCode, sw.ElapsedMilliseconds, text?.Length ?? 0);

                if (!resp.IsSuccessStatusCode && IsTransientStatus((int)resp.StatusCode) && attempt < MaxRetries)
                {
                    _logger.LogWarning("Transient status {Status}. Retrying attempt {Attempt}...", (int)resp.StatusCode, attempt + 1);
                    await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                    continue;
                }

                return (resp.StatusCode, resp.ReasonPhrase ?? "HTTP error", text);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < MaxRetries)
            {
                _logger.LogWarning("Request timeout on attempt {Attempt}. Retrying...", attempt);
                await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                continue;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Transient network error on attempt {Attempt}. Retrying...", attempt);
                await DelayWithJitterAsync(attempt, ct).ConfigureAwait(false);
                continue;
            }
        }

        return (0, "Exhausted retries", string.Empty);
    }

    private static bool IsTransientStatus(int status)
        => status is 408 or 429 or 500 or 502 or 503 or 504;

    private static Task DelayWithJitterAsync(int attempt, CancellationToken ct)
    {
        var baseMs = Math.Min(1000 * (int)Math.Pow(2, attempt - 1), 4000);
        var jitter = Random.Shared.Next(0, 250);
        return Task.Delay(baseMs + jitter, ct);
    }

    private static System.Net.Http.Headers.AuthenticationHeaderValue? GetDigestChallengeHeader(System.Net.Http.Headers.HttpHeaderValueCollection<System.Net.Http.Headers.AuthenticationHeaderValue> headers)
    {
        foreach (var h in headers)
            if (string.Equals(h.Scheme, "Digest", StringComparison.OrdinalIgnoreCase))
                return h;
        return null;
    }

    private System.Net.Http.Headers.AuthenticationHeaderValue BuildDigestAuthHeader(System.Net.Http.Headers.AuthenticationHeaderValue challenge, string method, string uriPathAndQuery)
    {
        var paramDict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parameters = challenge.Parameter ?? string.Empty;

        // Robust parse for key="value" pairs (values may contain commas)
        int i = 0;
        while (i < parameters.Length)
        {
            while (i < parameters.Length && (parameters[i] == ' ' || parameters[i] == ',')) i++;
            int startKey = i;
            while (i < parameters.Length && parameters[i] != '=' && parameters[i] != ',') i++;
            if (i >= parameters.Length || parameters[i] != '=') break;
            var key = parameters.Substring(startKey, i - startKey).Trim();
            i++;

            string val;
            if (i < parameters.Length && parameters[i] == '"')
            {
                i++;
                var innerSb = new StringBuilder();
                bool closed = false;
                while (i < parameters.Length)
                {
                    if (parameters[i] == '\\' && i + 1 < parameters.Length)
                    {
                        innerSb.Append(parameters[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (parameters[i] == '"')
                    {
                        closed = true;
                        i++;
                        break;
                    }
                    innerSb.Append(parameters[i]);
                    i++;
                }
                val = innerSb.ToString();
                if (!closed) break;
            }
            else
            {
                int startVal = i;
                while (i < parameters.Length && parameters[i] != ',') i++;
                val = parameters.Substring(startVal, i - startVal).Trim();
            }

            if (!string.IsNullOrWhiteSpace(key))
                paramDict[key] = val;
        }

        string realm = paramDict.TryGetValue("realm", out var r) ? r : "";
        string nonce = paramDict.TryGetValue("nonce", out var n) ? n : "";
        string qop = paramDict.TryGetValue("qop", out var q) ? q : "";
        string opaque = paramDict.TryGetValue("opaque", out var o) ? o : "";
        string algorithm = paramDict.TryGetValue("algorithm", out var a) ? a : "MD5";

        string cnonce = Guid.NewGuid().ToString("N");
        string nc = "00000001";

        string ha1;
        if (algorithm.Equals("MD5-sess", StringComparison.OrdinalIgnoreCase))
        {
            var ha1base = Md5Hex($"{_device.Username}:{realm}:{_device.Password}");
            ha1 = Md5Hex($"{ha1base}:{nonce}:{cnonce}");
        }
        else
        {
            ha1 = Md5Hex($"{_device.Username}:{realm}:{_device.Password}");
        }

        string ha2 = Md5Hex($"{method}:{uriPathAndQuery}");
        string response;

        var sb = new StringBuilder();
        sb.Append($"username=\"{_device.Username}\", ");
        sb.Append($"realm=\"{realm}\", ");
        sb.Append($"nonce=\"{nonce}\", ");
        sb.Append($"uri=\"{uriPathAndQuery}\", ");
        sb.Append($"algorithm={algorithm}, ");

        if (!string.IsNullOrEmpty(qop))
        {
            var qopToken = qop.Contains("auth", StringComparison.OrdinalIgnoreCase) ? "auth" : qop;
            response = Md5Hex($"{ha1}:{nonce}:{nc}:{cnonce}:{qopToken}:{ha2}");
            sb.Append($"response=\"{response}\", ");
            sb.Append($"qop={qopToken}, ");
            sb.Append($"nc={nc}, ");
            sb.Append($"cnonce=\"{cnonce}\"");
        }
        else
        {
            response = Md5Hex($"{ha1}:{nonce}:{ha2}");
            sb.Append($"response=\"{response}\"");
        }

        if (!string.IsNullOrEmpty(opaque))
            sb.Append($", opaque=\"{opaque}\"");

        return new System.Net.Http.Headers.AuthenticationHeaderValue("Digest", sb.ToString());
    }

    private static void ThrowIsapiException(HttpStatusCode status, string reason, string content)
    {
        try
        {
            var x = string.IsNullOrWhiteSpace(content) ? null : XDocument.Parse(content);

            string code = ((int)status).ToString();
            string msg = reason;

            if (x?.Root != null)
            {
                var codeEl = x.Root.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("statusCode", StringComparison.OrdinalIgnoreCase));
                var msgEl = x.Root.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("statusString", StringComparison.OrdinalIgnoreCase))
                          ?? x.Root.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("message", StringComparison.OrdinalIgnoreCase));

                if (codeEl != null) code = codeEl.Value;
                if (msgEl != null) msg = msgEl.Value;
            }

            throw new HikvisionIsapiException((int)status, $"{code}: {msg}", content);
        }
        catch (System.Xml.XmlException)
        {
            throw new HikvisionIsapiException((int)status, reason, content);
        }
    }

    private static string Md5Hex(string s)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.ASCII.GetBytes(s));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

public sealed class HikvisionIsapiException : Exception
{
    public int HttpStatus { get; }
    public string RawBody { get; }

    public HikvisionIsapiException(int httpStatus, string message, string rawBody) : base(message)
    {
        HttpStatus = httpStatus;
        RawBody = rawBody;
    }

    public override string ToString() => $"HTTP {HttpStatus}: {Message}\n{RawBody}";
}