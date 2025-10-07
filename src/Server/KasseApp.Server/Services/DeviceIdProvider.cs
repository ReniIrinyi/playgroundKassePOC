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
            if (OperatingSystem.IsWindows())
            {
                using var lm = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry64
                );
                using var key = lm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid") as string;
                if (!string.IsNullOrWhiteSpace(guid))
                    return "KASSA-" + HashShort(guid);
            }
        }
        catch
        {
        }

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "KasseApp", "device-id.txt"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path)) return File.ReadAllText(path).Trim();

        var id = "KASSA-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        File.WriteAllText(path, id);
        return id;
    }

    private static string HashShort(string s)
    {
        var sha1 = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(sha1.AsSpan(0, 8)); // 16 hex karakter
    }

}
