using KasseApp.Server;
using KasseApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("Dev");

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

app.MapGet("/api/device-id", async ([FromServices] DeviceIdProvider prov) =>
{
    var id = await prov.GetOrCreateAsync();
    return Results.Ok(new { deviceId = id });
});

app.MapPost("/api/login", async (
    [FromServices] DeviceIdProvider prov,
    [FromServices] PhpKasseClient php,
    [FromBody] LoginDto dto) =>
{
    var deviceId = await prov.GetOrCreateAsync();
    var tokenStr = await php.AuthenticateGetTokenStrAsync(deviceId, isTill: true);
    var tokenId  = PhpKasseClient.ExtractTokenId(tokenStr);
    var ok = await php.LoginAsync(tokenId, deviceId, dto.User, dto.Pass, dto.Licence, isTill: true);
    if (!ok) return Results.Unauthorized();
    var (authOk, raw) = await php.AuthStatusAsync(deviceId, isTill: true);
    return authOk ? Results.Ok(new { ok = true, raw }) : Results.Ok(new { ok = false, raw });
});
builder.Services.AddSingleton<EscPosService>();

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

public record PrintDto(string Text, bool Cut = true, bool OpenDrawerAfter = false);

public record LoginDto(string User, string Pass, string Licence);
