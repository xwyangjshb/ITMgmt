using System.ComponentModel.DataAnnotations;

namespace ITDeviceManager.Core.Models
{
    public class DeviceRelation
    {
        public int Id { get; set; }
        
        public int ParentDeviceId { get; set; }
        public virtual Device ParentDevice { get; set; } = null!;
        
        public int ChildDeviceId { get; set; }
        public virtual Device ChildDevice { get; set; } = null!;
        
        public RelationType RelationType { get; set; }
        
        [StringLength(200)]
        public string? Description { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public enum RelationType
    {
        Unknown = 0,
        PhysicalToVirtual = 1,  // 物理机到虚拟机
        NetworkSwitch = 2,      // 网络交换机连接
        PowerDependency = 3,    // 电源依赖关系
        ServiceDependency = 4   // 服务依赖关系
    }
}