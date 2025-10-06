using KasseApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseApp.Server;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> opt) : base(opt) { }

    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Device>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceId).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.DeviceId).IsUnique();
        });
    }
}