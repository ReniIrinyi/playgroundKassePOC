

using System.Text.Json;
using KasseApp.Server.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<EscPosService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("Dev");

static object JsonDocumentToObject(JsonDocument doc)
{
    return JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText())!;
}

var webRoot = builder.Configuration["WebRoot"]; 
if (!string.IsNullOrWhiteSpace(webRoot) && Directory.Exists(webRoot))
{
    var provider = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });

    app.MapFallback(context =>
    {
        var index = Path.Combine(webRoot, "index.html");
        if (File.Exists(index))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            return context.Response.SendFileAsync(index);
        }
        context.Response.StatusCode = 404;
        return Task.CompletedTask;
    });
}
else
{
    app.Logger.LogWarning("WebRoot not found. Static files disabled.");
}

app.MapGet("/api/device-id", () =>
{
    var id = DeviceIdProvider.GetDeviceId();
    return Results.Ok(new { deviceId = id });
});


app.MapPost("/api/login", async ([FromServices] CloudAuthClient cloud,
    [FromBody] LoginDto dto,
    CancellationToken ct) =>
{
    var deviceId = DeviceIdProvider.GetDeviceId();

    var (_, tokenId) = await cloud.AuthenticateAsync(ct);

    using var doc = await cloud.LoginAsync(tokenId, deviceId, isTill: true, dto.user, dto.pass, dto.licence, ct);

    return Results.Json(JsonDocumentToObject(doc));
});

app.MapGet("/api/auth/status", async ([FromServices] CloudAuthClient cloud, CancellationToken ct) =>
{
    using var doc = await cloud.GreenKasseAuthAsync(ct);
    return Results.Json(JsonDocumentToObject(doc));
});


app.MapPost("/api/print", ([FromServices] EscPosService esc, [FromBody] PrintDto dto) =>
{
    esc.PrintText(dto.Text ?? "", dto.Cut, dto.OpenDrawerAfter);
    return Results.Ok(new { printed = true });
});

app.MapPost("/api/drawer/open", ([FromServices] EscPosService esc) =>
{
    esc.OpenDrawer();
    return Results.Ok(new { opened = true });
});

app.Run();

record LoginDto(string user, string pass, string licence);

public record PrintDto(string Text, bool Cut = true, bool OpenDrawerAfter = false);

