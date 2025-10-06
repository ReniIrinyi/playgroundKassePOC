namespace KasseApp.Server.Services;

using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

public static class DeviceIdProvider
{
    public static string GetDeviceId()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = (string?)key?.GetValue("MachineGuid");
            if (!string.IsNullOrWhiteSpace(guid))
                return "KASSA-" + HashShort(guid);
        }
        catch { }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "KasseApp", "device-id.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) return File.ReadAllText(path).Trim();

        var id = "KASSA-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        File.WriteAllText(path, id);
        return id;
    }

    private static string HashShort(string s)
    {
        var sha1 = SHA1.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(sha1.AsSpan(0, 8));
    }
}
