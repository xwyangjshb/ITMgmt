using Microsoft.EntityFrameworkCore;
using ITDeviceManager.Core.Models;

namespace ITDeviceManager.API.Data
{
    public class DeviceContext : DbContext
    {
        public DeviceContext(DbContextOptions<DeviceContext> options) : base(options)
        {
            PowerOperations = Set<PowerOperation>();
            DeviceRelations = Set<DeviceRelation>();
            devices = Set<Device>();
        }
  

        private DbSet<Device> devices;

        public DbSet<Device> GetDevices()
        {
            return devices;
        }

        public void SetDevices(DbSet<Device> value)
        {
            devices = value;
        }

        public DbSet<DeviceRelation> DeviceRelations { get; set; }
        public DbSet<PowerOperation> PowerOperations { get; set; }
        
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
        }
    }
}