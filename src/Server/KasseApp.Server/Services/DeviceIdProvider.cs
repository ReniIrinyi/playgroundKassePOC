using KasseApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseApp.Server.Services;

public class DeviceIdProvider
{
    private readonly AppDb _db;

    public DeviceIdProvider(AppDb db)
    {
        _db = db;
    }

    public async Task<string> GetOrCreateAsync(CancellationToken ct = default)
    {
        var dev = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(ct);
        if (dev is not null) return dev.DeviceId;

        var deviceId = "POS-" + Guid.NewGuid().ToString("N");
        _db.Devices.Add(new Device { DeviceId = deviceId });
        await _db.SaveChangesAsync(ct);
        return deviceId;
    }
}