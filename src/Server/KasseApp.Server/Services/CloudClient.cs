namespace KasseApp.Server.Services;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

public sealed class CloudAuthClient
{
    private readonly HttpClient _http;
    private readonly CookieContainer _cookies = new();

    public CloudAuthClient(IConfiguration cfg)
    {
        var baseUrl = cfg["Cloud:BaseUrl"] ?? throw new InvalidOperationException("Cloud:BaseUrl missing");
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            AllowAutoRedirect = false,
            UseCookies = true
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl, UriKind.Absolute) };
    }

    public async Task<(string tokenStr, string tokenId)> AuthenticateAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/authenticate/");
        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Cloud authenticate failed: {(int)resp.StatusCode}");

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var tokenStr = json.GetProperty("tokenStr").GetString()!;
        var tokenId = tokenStr.Length >= 36 ? tokenStr[..36] : tokenStr;
        return (tokenStr, tokenId);
    }

    public async Task<JsonDocument> LoginAsync(string tokenId, string deviceId, bool isTill, string user, string pass, string licence, CancellationToken ct)
    {
        var secretObj = new { u = user, p = pass, l = licence };
        var secret = B64Url(JsonSerializer.Serialize(secretObj));
        var body = JsonContent.Create(new { secret });

        using var req = new HttpRequestMessage(HttpMethod.Put, "/login/")
        {
            Content = body
        };
        req.Headers.TryAddWithoutValidation("sessid", tokenId);
        req.Headers.TryAddWithoutValidation("device", deviceId);
        req.Headers.TryAddWithoutValidation("isTill", isTill ? "1" : "0");

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var txt = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Cloud login failed: {(int)resp.StatusCode} {txt}");
        }

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc;
    }

    public async Task<JsonDocument> GreenKasseAuthAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/greenKasse/auth/");
        var resp = await _http.SendAsync(req, ct);
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static string B64Url(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var b64 = Convert.ToBase64String(bytes);
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
