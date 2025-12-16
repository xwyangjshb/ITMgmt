using ITDeviceManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ITDeviceManager.API.Data;

public class DeviceContext : DbContext
{
    public DeviceContext(DbContextOptions<DeviceContext> options) : base(options)
    {
        PowerOperations = Set<PowerOperation>();
        DeviceRelations = Set<DeviceRelation>();
        _devices = Set<Device>();
        Users = Set<User>();
    }


    private DbSet<Device> _devices;

    public DbSet<Device> GetDevices()
    {
        return _devices;
    }

    public void SetDevices(DbSet<Device> value)
    {
        _devices = value;
    }

    public DbSet<DeviceRelation> DeviceRelations { get; set; }
    public DbSet<PowerOperation> PowerOperations { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<MagicPacketCapture> MagicPacketCaptures { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Device 配置
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IPAddress).IsUnique();
            entity.HasIndex(e => e.MACAddress).IsUnique();
            entity.Property(e => e.Status).HasConversion<int>();
        });

        // DeviceRelation 配置
        modelBuilder.Entity<DeviceRelation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ParentDevice)
                .WithMany(e => e.ParentRelations)
                .HasForeignKey(e => e.ParentDeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ChildDevice)
                .WithMany(e => e.ChildRelations)
                .HasForeignKey(e => e.ChildDeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.RelationType).HasConversion<int>();

            // 防止循环引用
            entity.HasIndex(e => new { e.ParentDeviceId, e.ChildDeviceId }).IsUnique();
        });

        // PowerOperation 配置
        modelBuilder.Entity<PowerOperation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Device)
                .WithMany(e => e.PowerOperations)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Operation).HasConversion<int>();
            entity.Property(e => e.Result).HasConversion<int>();
        });

        // User 配置
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Role).HasDefaultValue("User");
        });

        // MagicPacketCapture 配置
        modelBuilder.Entity<MagicPacketCapture>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CapturedAt);
            entity.HasIndex(e => e.TargetMACAddress);

            entity.HasOne(e => e.MatchedDevice)
                  .WithMany()
                  .HasForeignKey(e => e.MatchedDeviceId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
