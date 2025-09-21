using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITDeviceManager.API.Data;
using ITDeviceManager.Core.Models;
using ITDeviceManager.Core.Services;

namespace ITDeviceManager.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevicesController : ControllerBase
    {
        private readonly DeviceContext _context;
        private readonly INetworkDiscoveryService _discoveryService;
        private readonly IWakeOnLanService _wakeOnLanService;

        public DevicesController(
            DeviceContext context, 
            INetworkDiscoveryService discoveryService,
            IWakeOnLanService wakeOnLanService)
        {
            _context = context;
            _discoveryService = discoveryService;
            _wakeOnLanService = wakeOnLanService;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Device>>> GetDevices()
        {
            try
            {
                var devices = await _context.Set<Device>()
                    .Select(d => new Device
                    {
                        Id = d.Id,
                        Name = d.Name,
                        IPAddress = d.IPAddress,
                        MACAddress = d.MACAddress,
                        DeviceType = d.DeviceType,
                        OperatingSystem = d.OperatingSystem,
                        Manufacturer = d.Manufacturer,
                        Model = d.Model,
                        Status = d.Status,
                        LastSeen = d.LastSeen,
                        CreatedAt = d.CreatedAt,
                        UpdatedAt = d.UpdatedAt,
                        WakeOnLanEnabled = d.WakeOnLanEnabled,
                        Description = d.Description,
                        // 不包含导航属性，避免循环引用
                        PowerOperations = new List<PowerOperation>(),
                        ParentRelations = new List<DeviceRelation>(),
                        ChildRelations = new List<DeviceRelation>()
                    })
                    .ToListAsync();
                
                return Ok(devices);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                Console.WriteLine($"获取设备列表时发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                
                // 返回空列表而不是错误，避免前端解析问题
                return Ok(new List<Device>());
            }
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<Device>> GetDevice(int id)
        {
            try
            {
                var device = await _context.Set<Device>()
                    .Select(d => new Device
                    {
                        Id = d.Id,
                        Name = d.Name,
                        IPAddress = d.IPAddress,
                        MACAddress = d.MACAddress,
                        DeviceType = d.DeviceType,
                        OperatingSystem = d.OperatingSystem,
                        Manufacturer = d.Manufacturer,
                        Model = d.Model,
                        Status = d.Status,
                        LastSeen = d.LastSeen,
                        CreatedAt = d.CreatedAt,
                        UpdatedAt = d.UpdatedAt,
                        WakeOnLanEnabled = d.WakeOnLanEnabled,
                        Description = d.Description,
                        // 不包含导航属性，避免循环引用
                        PowerOperations = new List<PowerOperation>(),
                        ParentRelations = new List<DeviceRelation>(),
                        ChildRelations = new List<DeviceRelation>()
                    })
                    .FirstOrDefaultAsync(d => d.Id == id);
                    
                if (device == null)
                {
                    return NotFound();
                }
                
                return Ok(device);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                Console.WriteLine($"获取设备详情时发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                
                return StatusCode(500, new { error = "获取设备详情失败", message = ex.Message });
            }
        }
        
        [HttpPost]
        public async Task<ActionResult<Device>> CreateDevice(Device device)
        {
            try
            {
                // 检查MAC地址是否已存在
                var existingDeviceByMac = await _context.Set<Device>()
                    .FirstOrDefaultAsync(d => d.MACAddress == device.MACAddress);
                
                if (existingDeviceByMac != null)
                {
                    return Conflict(new { 
                        error = "MAC地址已存在", 
                        message = $"MAC地址 {device.MACAddress} 已被设备 {existingDeviceByMac.Name} (ID: {existingDeviceByMac.Id}) 使用",
                        existingDevice = new {
                            id = existingDeviceByMac.Id,
                            name = existingDeviceByMac.Name,
                            ipAddress = existingDeviceByMac.IPAddress,
                            macAddress = existingDeviceByMac.MACAddress
                        }
                    });
                }
                
                // 检查IP地址是否已存在
                var existingDeviceByIp = await _context.Set<Device>()
                    .FirstOrDefaultAsync(d => d.IPAddress == device.IPAddress);
                
                if (existingDeviceByIp != null)
                {
                    return Conflict(new { 
                        error = "IP地址已存在", 
                        message = $"IP地址 {device.IPAddress} 已被设备 {existingDeviceByIp.Name} (ID: {existingDeviceByIp.Id}) 使用",
                        existingDevice = new {
                            id = existingDeviceByIp.Id,
                            name = existingDeviceByIp.Name,
                            ipAddress = existingDeviceByIp.IPAddress,
                            macAddress = existingDeviceByIp.MACAddress
                        }
                    });
                }
                
                device.CreatedAt = DateTime.UtcNow;
                device.UpdatedAt = DateTime.UtcNow;
                
                _context.Set<Device>().Add(device);
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"成功创建设备: {device.Name} (MAC: {device.MACAddress}, IP: {device.IPAddress})");
                return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, device);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建设备时发生错误: {ex.Message}");
                return StatusCode(500, new { error = "创建设备失败", message = ex.Message });
            }
        }
        
        [HttpPatch("{id}/wake-on-lan")]
        public async Task<IActionResult> UpdateWakeOnLanSetting(int id, [FromBody] bool enabled)
        {
            try
            {
                var device = await _context.Set<Device>().FindAsync(id);
                if (device == null)
                {
                    return NotFound();
                }
                
                device.WakeOnLanEnabled = enabled;
                device.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"设备 {id} Wake-on-LAN 设置已更新为: {enabled}");
                return Ok(new { id = device.Id, wakeOnLanEnabled = device.WakeOnLanEnabled });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新设备 {id} Wake-on-LAN 设置时发生错误: {ex.Message}");
                return StatusCode(500, new { error = "更新Wake-on-LAN设置失败", message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDevice(int id, Device device)
        {
            try
            {
                if (id != device.Id)
                {
                    return BadRequest("ID mismatch");
                }
                
                var existingDevice = await _context.Set<Device>().FindAsync(id);
                if (existingDevice == null)
                {
                    return NotFound();
                }
                
                // 更新设备属性
                existingDevice.Name = device.Name;
                existingDevice.IPAddress = device.IPAddress;
                existingDevice.MACAddress = device.MACAddress;
                existingDevice.DeviceType = device.DeviceType;
                existingDevice.OperatingSystem = device.OperatingSystem;
                existingDevice.Manufacturer = device.Manufacturer;
                existingDevice.Model = device.Model;
                existingDevice.Status = device.Status;
                existingDevice.WakeOnLanEnabled = device.WakeOnLanEnabled;
                existingDevice.Description = device.Description;
                existingDevice.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"设备 {id} 更新成功，Wake-on-LAN: {device.WakeOnLanEnabled}");
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新设备 {id} 时发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return StatusCode(500, new { error = "更新设备失败", message = ex.Message });
            }
        }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            var device = await _context.Set<Device>().FindAsync(id);
            if (device == null)
            {
                return NotFound();
            }
            
            _context.Set<Device>().Remove(device);
            await _context.SaveChangesAsync();
            
            return NoContent();
        }
        
        [HttpPost("discover")]
        public async Task<ActionResult<IEnumerable<Device>>> DiscoverDevices([FromBody] DiscoveryRequest request)
        {
            try
            {
                var discoveredDevices = await _discoveryService.DiscoverDevicesAsync(request.NetworkRange);
                var newDevices = new List<Device>();
                var updatedDevices = new List<Device>();
                
                foreach (var device in discoveredDevices)
                {
                    var existingDevice = await _context.Set<Device>()
                        .FirstOrDefaultAsync(d => d.MACAddress == device.MACAddress);
                        
                    if (existingDevice == null)
                    {
                        // 再次检查IP地址是否被其他设备使用
                        var deviceWithSameIp = await _context.Set<Device>()
                            .FirstOrDefaultAsync(d => d.IPAddress == device.IPAddress);
                            
                        if (deviceWithSameIp != null)
                        {
                            Console.WriteLine($"[警告] 发现IP冲突: 新设备 {device.Name} (MAC: {device.MACAddress}) 与现有设备 {deviceWithSameIp.Name} (MAC: {deviceWithSameIp.MACAddress}) 使用相同IP {device.IPAddress}");
                            // 更新现有设备的MAC地址（可能是设备更换了网卡）
                            deviceWithSameIp.MACAddress = device.MACAddress;
                            deviceWithSameIp.Name = device.Name;
                            deviceWithSameIp.DeviceType = device.DeviceType;
                            deviceWithSameIp.Status = device.Status;
                            deviceWithSameIp.LastSeen = DateTime.UtcNow;
                            deviceWithSameIp.UpdatedAt = DateTime.UtcNow;
                            updatedDevices.Add(deviceWithSameIp);
                            continue;
                        }
                        
                        device.CreatedAt = DateTime.UtcNow;
                        device.UpdatedAt = DateTime.UtcNow;
                        _context.Set<Device>().Add(device); 
                        newDevices.Add(device);
                        Console.WriteLine($"发现新设备: {device.Name} (MAC: {device.MACAddress}, IP: {device.IPAddress})");
                    }
                    else
                    {
                        // 更新现有设备信息
                        bool hasChanges = false;
                        
                        if (existingDevice.IPAddress != device.IPAddress)
                        {
                            Console.WriteLine($"设备 {existingDevice.Name} IP地址变更: {existingDevice.IPAddress} -> {device.IPAddress}");
                            existingDevice.IPAddress = device.IPAddress;
                            hasChanges = true;
                        }
                        
                        if (existingDevice.Status != device.Status)
                        {
                            existingDevice.Status = device.Status;
                            hasChanges = true;
                        }
                        
                        if (existingDevice.DeviceType != device.DeviceType && device.DeviceType != DeviceType.Unknown)
                        {
                            existingDevice.DeviceType = device.DeviceType;
                            hasChanges = true;
                        }
                        
                        if (hasChanges)
                        {
                            existingDevice.LastSeen = DateTime.UtcNow;
                            existingDevice.UpdatedAt = DateTime.UtcNow;
                            updatedDevices.Add(existingDevice);
                        }
                    }
                }
                
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"设备发现完成: 新增 {newDevices.Count} 个设备，更新 {updatedDevices.Count} 个设备");
                return Ok(new { 
                    newDevices = newDevices,
                    updatedDevices = updatedDevices.Count,
                    message = $"发现完成: 新增 {newDevices.Count} 个设备，更新 {updatedDevices.Count} 个设备"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设备发现过程中发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return StatusCode(500, new { error = "设备发现失败", message = ex.Message });
            }
        }
        /// <summary>
        /// 发送WOL包唤醒设备
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("{id}/wake")]
        public async Task<ActionResult<PowerOperation>> WakeDevice(int id)
        {
            var device = await _context.Set<Device>().FindAsync(id);
            if (device == null)
            {
                return NotFound();
            }
            
            // 检查WOL是否已启用
            if (!device.WakeOnLanEnabled)
            {
                return BadRequest(new { error = "Wake-on-LAN is not enabled for this device" });
            }
            
            var operation = await _wakeOnLanService.CreateWakeOperationAsync(id, "API");
            _context.PowerOperations.Add(operation);
            await _context.SaveChangesAsync();
            
            var success = await _wakeOnLanService.WakeDeviceAsync(device);
            
            operation.Result = success ? PowerOperationResult.Success : PowerOperationResult.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ResultMessage = success ? "Wake-on-LAN packet sent successfully" : "Failed to send Wake-on-LAN packet";
            
            await _context.SaveChangesAsync();
            
            return Ok(operation);
        }
        
        [HttpPost("{id}/shutdown")]
        public async Task<ActionResult<PowerOperation>> ShutdownDevice(int id)
        {
            var device = await _context.Set<Device>().FindAsync(id);
            if (device == null)
            {
                return NotFound();
            }
            
            var operation = new PowerOperation
            {
                DeviceId = id,
                Operation = PowerOperationType.Shutdown,
                Result = PowerOperationResult.Pending,
                RequestedBy = "API",
                RequestedAt = DateTime.UtcNow
            };
            
            _context.PowerOperations.Add(operation);
            await _context.SaveChangesAsync();
            
            var success = await _wakeOnLanService.ShutdownDeviceAsync(device.IPAddress);
            
            operation.Result = success ? PowerOperationResult.Success : PowerOperationResult.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ResultMessage = success ? "Shutdown command sent successfully" : "Failed to send shutdown command";
            
            await _context.SaveChangesAsync();
            
            return Ok(operation);
        }
        
        private bool DeviceExists(int id)
        {
            return _context.Set<Device>().Any(e => e.Id == id);
        }
    }
    
    public class DiscoveryRequest
    {
        public string NetworkRange { get; set; } = string.Empty;
    }
    
    public class ShutdownRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}