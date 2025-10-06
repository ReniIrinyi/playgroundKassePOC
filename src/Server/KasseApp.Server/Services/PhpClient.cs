using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KasseApp.Server.Services;

public class PhpKasseClient
{
    private readonly HttpClient _http;
    private readonly CookieContainer _cookies = new();
    private readonly string _authPath;
    private readonly string _loginPath;
    private readonly string _authenticatePath;

    public PhpKasseClient(IConfiguration cfg)
    {
        var baseUrl = cfg["PhpCloud:BaseUrl"] ?? throw new InvalidOperationException("PhpCloud:BaseUrl missing");
        _authPath = cfg["PhpCloud:GreenKasseAuthPath"] ?? "/greenKasse/auth";
        _loginPath = cfg["PhpCloud:LoginPath"] ?? "/login";
        _authenticatePath = cfg["PhpCloud:AuthenticatePath"] ?? "/authenticate";

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.All
        };

        _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
    }

    private static string B64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<string> AuthenticateGetTokenStrAsync(string deviceId, bool isTill = true, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, _authenticatePath);
        req.Headers.TryAddWithoutValidation("device", deviceId);
        if (isTill) req.Headers.TryAddWithoutValidation("isTill", "1");
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public static string ExtractTokenId(string tokenStr)
    {
        var m = Regex.Match(tokenStr, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        if (!m.Success) throw new InvalidOperationException("tokenId not found in tokenStr");
        return m.Value;
    }

    public async Task<bool> LoginAsync(string tokenId, string deviceId, string user, string pass, string licence, bool isTill = true, CancellationToken ct = default)
    {
        var secretJson = JsonSerializer.Serialize(new { u = user, p = pass, l = licence });
        var secret = B64Url(Encoding.UTF8.GetBytes(secretJson));

        var req = new HttpRequestMessage(HttpMethod.Put, _loginPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { secret }), Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("sessid", tokenId);
        req.Headers.TryAddWithoutValidation("device", deviceId);
        if (isTill) req.Headers.TryAddWithoutValidation("isTill", "1");

        var resp = await _http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<(bool ok, string raw)> AuthStatusAsync(string deviceId, bool isTill = true, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, _authPath);
        req.Headers.TryAddWithoutValidation("device", deviceId);
        if (isTill) req.Headers.TryAddWithoutValidation("isTill", "1");
        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return (resp.IsSuccessStatusCode, body);
    }
}
