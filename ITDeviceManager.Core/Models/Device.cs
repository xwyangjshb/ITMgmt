using System.ComponentModel.DataAnnotations;

namespace ITDeviceManager.Core.Models
{
    public class Device
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(15)]
        public string IPAddress { get; set; } = string.Empty;
        
        [Required]
        [StringLength(17)]
        public string MACAddress { get; set; } = string.Empty;
        
        [StringLength(50)]
        public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
        
        [StringLength(100)]
        public string? OperatingSystem { get; set; }
        
        [StringLength(100)]
        public string? Manufacturer { get; set; }
        
        [StringLength(100)]
        public string? Model { get; set; }
        
        public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;
        
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public bool WakeOnLanEnabled { get; set; } = false;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        // 导航属性
        public virtual ICollection<DeviceRelation> ParentRelations { get; set; } = new List<DeviceRelation>();
        public virtual ICollection<DeviceRelation> ChildRelations { get; set; } = new List<DeviceRelation>();
        public virtual ICollection<PowerOperation> PowerOperations { get; set; } = new List<PowerOperation>();
    }
    
    public enum DeviceStatus
    {
        Unknown = 0,
        Online = 1,
        Offline = 2,
        Maintenance = 3,
        Error = 4
    }
    
    public enum DeviceType
    {
        Unknown = 0,
        Computer = 1,
        Server = 2,
        Router = 3,
        Switch = 4,
        Printer = 5,
        Phone = 6,
        Tablet = 7,
        IoT = 8,
        Camera = 9,
        AccessPoint = 10
    }
}