using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.PointOfService;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PosDeviceApi
{
    internal class Program
    {
        private static HttpListener http;
        private static PosExplorer _ex;
        private static PosPrinter Printer;
        private static CashDrawer Drawer;
        private static LineDisplay Display;
        private static string Url;

        private static int LastHttpErr;
        private static string LastHttpMsg = "";

        private static PosExplorer Ex
        {
            get
            {
                if (_ex != null) return _ex;
                try
                {
                    _ex = new PosExplorer();
                    return _ex;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("PosExplorer init failed: " + ex);
                    throw;
                }
            }
        }

        private static string LdnPrinter => Get("LdnPrinter", "PosPrinter1");
        private static string LdnDrawer => Get("LdnDrawer", "CashDrawer1");
        private static string LdnDisplay => Get("LdnDisplay", "LineDisplay1");
        private static string WebRoot => Get("WebRoot", null);

        private static void Main()
        {
            Url = Get("HttpUrl", "http://localhost:5005/");

            // telemetry fix
            EnsurePos4NetTelemetryKeys();

            Url = EnsureHttpListenerAndPort(Url);


            Console.WriteLine("Starting Device API on " + Url);
            Console.WriteLine($"LDNs: printer={LdnPrinter}, drawer={LdnDrawer}, display={LdnDisplay}");

            Console.Title = "POS for .NET — Device API";
            Console.WriteLine("Starting Device API on " + Url);
            Console.WriteLine($"LDNs: printer={LdnPrinter}, drawer={LdnDrawer}, display={LdnDisplay}");
            Console.WriteLine(
                "If startup fails, run once as admin:\n" +
                "  netsh http add urlacl url=" + Url.Replace("localhost", "+") + " user=Users\n"
            );
            AppDomain.CurrentDomain.ProcessExit += (_, __) => Cleanup();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Cleanup();
                Environment.Exit(0);
            };


            try
            {
                while (http != null && http.IsListening)
                {
                    var ctx = http.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
                }
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine("Listener stopped: " + ex.Message);
            }
        }

        private static void Cleanup()
        {
            try
            {
                if (Display != null)
                {
                    Display.DeviceEnabled = false;
                    Display.Release();
                    Display.Close();
                    Display = null;
                }
            }
            catch
            {
            }

            try
            {
                if (Drawer != null)
                {
                    Drawer.DeviceEnabled = false;
                    Drawer.Release();
                    Drawer.Close();
                    Drawer = null;
                }
            }
            catch
            {
            }

            try
            {
                if (Printer != null)
                {
                    Printer.DeviceEnabled = false;
                    Printer.Release();
                    Printer.Close();
                    Printer = null;
                }
            }
            catch
            {
            }
        }

        private static object ListDevices()
        {
            var ex = Ex;

            var printers = Ex.GetDevices(DeviceType.PosPrinter)
                .Cast<DeviceInfo>()
                .Select(d => new { d.ServiceObjectName, d });


            var drawers = ex.GetDevices(DeviceType.CashDrawer)
                .Cast<DeviceInfo>()
                .Select(d => new { d.ServiceObjectName, d });

            var displays = ex.GetDevices(DeviceType.LineDisplay)
                .Cast<DeviceInfo>()
                .Select(d => new { d.ServiceObjectName, d });

            return new { printers, drawers, displays };
        }


        private static bool Run(string file, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                });
                p?.WaitForExit();
                return p?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string EnsureHttpListenerAndPort(string url)
        {
            var u = new Uri(url);
            var host = u.Host;
            var startPort = u.Port;

            for (var port = startPort; port < startPort + 10; port++)
            {
                var tryUrl = $"http://{host}:{port}/";
                if (TryStartHttp(tryUrl)) return tryUrl;

                if (LastHttpErr == 5 /*Access denied*/ || IsPrefixConflictMessage(LastHttpMsg))
                    if (TryFixUrlAcl(tryUrl))
                        if (TryStartHttp(tryUrl))
                            return tryUrl;

                Console.WriteLine($"Port {port} besetzt, try: {port + 1}");
            }

            throw new Exception("Das sollte nicht passieren.. Keine Ports sind verfügbar.");
        }

        private static bool TryStartHttp(string url)
        {
            try
            {
                http = new HttpListener();
                http.Prefixes.Add(url);
                http.Start();
                return true;
            }
            catch (HttpListenerException ex)
            {
                LastHttpErr = ex.ErrorCode;
                LastHttpMsg = ex.Message;
                Console.WriteLine($"HttpListener start failed ({ex.ErrorCode}): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LastHttpErr = -1;
                LastHttpMsg = ex.Message;
                Console.WriteLine("HttpListener start failed: " + ex);
                return false;
            }
        }

        private static bool IsPrefixConflictMessage(string msg)
        {
            var m = (msg ?? "").ToLowerInvariant();
            return m.Contains("konflikt") || m.Contains("conflict") || m.Contains("already");
        }
        
        private static bool TryFixUrlAcl(string url)
        {
            if (!IsAdmin())
            {
                Console.WriteLine("URL ACL setup kann nur Admin schreiben. Bitte als Admin ausführen.");
                return false;
            }

            var aclUrl = url.Replace("localhost", "+"); 
            Run("netsh", $"http delete urlacl url={aclUrl}");
            var ok = Run("netsh", $"http add urlacl url={aclUrl} user=Users");
            Console.WriteLine(ok ? "URL ACL setup ok: " + aclUrl : "URL ACL setup fehlgeschlagen");
            return ok;
        }


        private static void EnsurePos4NetTelemetryKeys()
        {
            try
            {
                // HKLM x64 és x86 (WOW6432Node) 
                SetDword(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\POSfor.NET\Telemetry",
                    "OptIn", 0);
                SetDword(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\POSfor.NET\Telemetry",
                    "OptIn", 0);

                // HKCU
                using (var cu = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\POSfor.NET\Telemetry", true))
                {
                    cu?.SetValue("OptIn", 0, RegistryValueKind.DWord);
                }
                
                SetDword(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\SQMClient\Windows",
                    "CEIPEnable", 0);

                Console.WriteLine("POS for .NET telemetry keys: OK");
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine(
                    "Ich kann nicht in registry schreiben. Bitte als Admin ausführen.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Telemetry einstellungen fehlgeschlagen: " + ex.Message);
            }
        }

        private static void SetDword(RegistryHive hive, RegistryView view, string subkey, string name, int value)
        {
            var baseKey = RegistryKey.OpenBaseKey(hive, view);
            var key = baseKey.CreateSubKey(subkey, true);
            if (key == null) throw new Exception($"Nem hozható létre: {hive}\\{subkey}");
            var cur = key.GetValue(name);
            if (!(cur is int i) || i != value)
                key.SetValue(name, value, RegistryValueKind.DWord);
        }
        

        private static bool IsAdmin()
        {
            var id = WindowsIdentity.GetCurrent();
            var p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void Handle(HttpListenerContext ctx)
        {
            try
            {
                // CORS
                ctx.Response.AddHeader("Access-Control-Allow-Origin", Get("CorsAllowOrigin", "*"));
                ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                if (TryServeStatic(ctx)) return;

                var path = ctx.Request.Url.AbsolutePath.ToLowerInvariant();

                if (path == "/api/ping")
                {
                    Json(ctx, new { ok = true, at = DateTimeOffset.Now });
                    return;
                }

                if (path == "/api/debug")
                {
                    Json(ctx, new
                    {
                        ok = true,
                        at = DateTimeOffset.Now,
                        devices = ListDevices()
                    });
                    return;
                }


                if (path == "/api/config")
                {
                    Json(ctx,
                        new { url = Url, ldnPrinter = LdnPrinter, ldnDrawer = LdnDrawer, ldnDisplay = LdnDisplay });
                    return;
                }

                if (path == "/api/printer/open")
                {
                    EnsurePrinter();
                    Json(ctx, new { opened = Printer?.DeviceEnabled == true });
                    return;
                }

                if (path == "/api/printer/print" && ctx.Request.HttpMethod == "POST")
                {
                    EnsurePrinter();
                    var body = ReadJson(ctx);
                    var text = (string)body["text"] ?? "";
                    var cut = (bool?)body["cut"] ?? true;
                    var openDrawerAfter = (bool?)body["openDrawerAfter"] ?? false;

                    if (!string.IsNullOrEmpty(text))
                        Printer.PrintNormal(PrinterStation.Receipt, text.Replace("\n", "\n"));

                    if (openDrawerAfter) TryOpenDrawer();
                    if (cut) Printer.CutPaper(100);

                    Json(ctx, new { printed = true });
                    return;
                }

                if (path == "/api/drawer/open" && ctx.Request.HttpMethod == "POST")
                {
                    TryOpenDrawer();
                    Json(ctx, new { opened = true });
                    return;
                }

                if (path == "/api/display/open")
                {
                    EnsureDisplay();
                    Json(ctx, new { opened = Display?.DeviceEnabled == true });
                    return;
                }

                if (path == "/api/display/text" && ctx.Request.HttpMethod == "POST")
                {
                    EnsureDisplay();
                    var body = ReadJson(ctx);
                    var line1 = (string)body["line1"] ?? "";
                    var line2 = (string)body["line2"];
                    var clear = (bool?)body["clearFirst"] ?? true;

                    if (clear) Display.ClearText();
                    Display.DisplayText(line1, DisplayTextMode.Normal);
                    if (!string.IsNullOrEmpty(line2) && Display.Rows >= 2)
                        Display.DisplayTextAt(1, 0, line2);

                    Json(ctx, new { displayed = true });
                    return;
                }

                ctx.Response.StatusCode = 404;
                Write(ctx, "Not found");
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                Write(ctx, ex.ToString());
                ctx.Response.Close();
            }
        }


        private static void EnsurePrinter(int retries = 1)
        {
            if (Printer != null && Printer.DeviceEnabled) return;

            for (var i = 0; i <= retries; i++)
                try
                {
                    if (Printer != null)
                    {
                        try
                        {
                            Printer.Close();
                        }
                        catch
                        {
                        }

                        Printer = null;
                    }

                    var dev = Ex.GetDevice(DeviceType.PosPrinter, LdnPrinter)
                              ?? throw new Exception($"PosPrinter LDN '{LdnPrinter}' not found.");
                    Printer = (PosPrinter)Ex.CreateInstance(dev);
                    Printer.Open();
                    Printer.Claim(5000);
                    Printer.DeviceEnabled = true;
                    return;
                }
                catch (PosControlException)
                {
                    if (i == retries) throw;
                    Thread.Sleep(300);
                }
        }


        private static void EnsureDrawer(int retries = 1)
        {
            if (Drawer != null && Drawer.DeviceEnabled) return;

            for (var i = 0; i <= retries; i++)
                try
                {
                    if (Drawer != null)
                    {
                        try
                        {
                            Drawer.Close();
                        }
                        catch
                        {
                        }

                        Drawer = null;
                    }

                    var dev = Ex.GetDevice(DeviceType.CashDrawer, LdnDrawer)
                              ?? throw new Exception($"CashDrawer LDN '{LdnDrawer}' not found.");
                    Drawer = (CashDrawer)Ex.CreateInstance(dev);
                    Drawer.Open();
                    Drawer.Claim(5000); // 5s – legyen elég idő
                    Drawer.DeviceEnabled = true;
                    return;
                }
                catch (PosControlException)
                {
                    if (i == retries) throw;
                    Thread.Sleep(300);
                }
        }


        private static void EnsureDisplay()
        {
            if (Display != null && Display.DeviceEnabled) return;
            var dev = Ex.GetDevice(DeviceType.LineDisplay, LdnDisplay);
            Display = (LineDisplay)Ex.CreateInstance(dev);
            Display.Open();
            Display.Claim(1000);
            Display.DeviceEnabled = true;
        }

        private static void TryOpenDrawer(bool preferDrawerDevice = true)
        {
            if (preferDrawerDevice)
                try
                {
                    EnsureDrawer();
                    Drawer.OpenDrawer();
                    Drawer.DeviceEnabled = false;
                    Drawer.Release();
                    Drawer.Close();
                    Drawer = null;
                    return;
                }
                catch
                {
                    
                }
            
            Printer.PrintNormal(PrinterStation.Receipt, "\x1B\x70\x00\x64\x64");
        }


        private static string Get(string key, string def)
        {
            return ConfigurationManager.AppSettings[key] ?? def;
        }

        private static JObject ReadJson(HttpListenerContext ctx)
        {
            using (var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                var s = sr.ReadToEnd();
                if (string.IsNullOrWhiteSpace(s)) return new JObject();
                return JsonConvert.DeserializeObject<JObject>(s) ?? new JObject();
            }
        }


        private static void Json(HttpListenerContext ctx, object obj)
        {
            var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.OutputStream.Write(payload, 0, payload.Length);
            ctx.Response.Close();
        }

        private static void Write(HttpListenerContext ctx, string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.OutputStream.Write(b, 0, b.Length);
        }

        private static bool TryServeStatic(HttpListenerContext ctx)
        {
            if (string.IsNullOrEmpty(WebRoot)) return false;

            var urlPath = ctx.Request.Url.AbsolutePath;
            if (urlPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase)) return false;

            if (urlPath == "/" || string.IsNullOrEmpty(urlPath))
                urlPath = "/index.html";

            var root = Path.GetFullPath(WebRoot);
            var full = Path.GetFullPath(
                Path.Combine(root, urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))
            );

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                return false;

            var ext = Path.GetExtension(full).ToLowerInvariant();
            string ct;
            switch (ext)
            {
                case ".html": ct = "text/html; charset=utf-8"; break;
                case ".js": ct = "text/javascript; charset=utf-8"; break;
                case ".css": ct = "text/css; charset=utf-8"; break;
                case ".json": ct = "application/json; charset=utf-8"; break;
                case ".svg": ct = "image/svg+xml"; break;
                case ".png": ct = "image/png"; break;
                case ".jpg":
                case ".jpeg": ct = "image/jpeg"; break;
                case ".ico": ct = "image/x-icon"; break;
                case ".woff2": ct = "font/woff2"; break;
                case ".webmanifest":
                case ".manifest": ct = "application/manifest+json"; break;
                default: ct = "application/octet-stream"; break;
            }


            var bytes = File.ReadAllBytes(full);
            ctx.Response.ContentType = ct;
            if (Path.GetFileName(full) == "sw.js")
                ctx.Response.Headers["Service-Worker-Allowed"] = "/"; 
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
            return true;
        }
    }
}